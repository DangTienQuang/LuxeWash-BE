using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
using BLL.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class VoucherCampaignService : IVoucherCampaignService
    {
        private readonly AutoWashDbContext _context;
        private readonly IEmailService _emailService;

        public VoucherCampaignService(AutoWashDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<CampaignVoucherResponseDTO> CreateBirthdayVouchersAsync(CreateBirthdayVouchersDTO request)
        {
            var voucher = await CreateCampaignVoucherAsync(request, VoucherCampaignType.Birthday);
            return MapCampaignDto(voucher);
        }

        public async Task<CampaignVoucherResponseDTO> CreateAgeVouchersAsync(CreateAgeVouchersDTO request)
        {
            if (request.TargetAge <= 0) throw new BadRequestException("Invalid target age.");
            var voucher = await CreateCampaignVoucherAsync(request, VoucherCampaignType.Age, v => v.TargetAge = request.TargetAge);
            return MapCampaignDto(voucher);
        }

        public async Task<CampaignVoucherResponseDTO> CreateWinbackVouchersAsync(CreateWinbackVouchersDTO request)
        {
            if (request.ResendAfterDays > request.InactiveDays)
                throw new BadRequestException("Resend days should not be greater than the days of inactivity.");

            var voucher = await CreateCampaignVoucherAsync(request, VoucherCampaignType.Winback, v =>
            {
                v.InactiveDays = request.InactiveDays;
                v.ResendAfterDays = request.ResendAfterDays;
            });
            return MapCampaignDto(voucher);
        }

        public async Task<CampaignVoucherResponseDTO> CreateVipVouchersAsync(CreateVipVouchersDTO request)
        {
            if (!request.RequiredTierId.HasValue) throw new BadRequestException("Please select the applicable VIP tier.");
            var voucher = await CreateCampaignVoucherAsync(request, VoucherCampaignType.Vip, v => v.RequiredTierId = request.RequiredTierId);
            var response = MapCampaignDto(voucher);

            if (CanProcessCampaignNow(voucher))
            {
                var result = await ProcessCampaignAsync(voucher, DateTime.UtcNow.ToVnTime().Date);
                response.ScannedUsers = result.ScannedUsers;
                response.GrantedCount = result.GrantedCount;
                response.SkippedCount = result.SkippedCount;
            }

            return response;
        }

        public async Task<CampaignVoucherResponseDTO> CreateMilestoneVouchersAsync(CreateMilestoneVouchersDTO request)
        {
            if (request.MilestoneUsageCount <= 0) throw new BadRequestException("Invalid usage milestone.");
            var voucher = await CreateCampaignVoucherAsync(request, VoucherCampaignType.Milestone, v => v.MilestoneUsageCount = request.MilestoneUsageCount);
            return MapCampaignDto(voucher);
        }

        public async Task<List<VoucherCampaignProcessResultDTO>> ProcessDailyCampaignsAsync(DateTime? targetDate = null)
        {
            var now = DateTime.UtcNow;
            var date = (targetDate ?? DateTime.UtcNow.ToVnTime()).Date;
            var activeCampaigns = await GetActiveCampaignsQuery(now)
                .Where(v => v.CampaignType == VoucherCampaignType.Birthday
                         || v.CampaignType == VoucherCampaignType.Age
                         || v.CampaignType == VoucherCampaignType.Winback
                         || v.CampaignType == VoucherCampaignType.Vip)
                .ToListAsync();

            var results = new List<VoucherCampaignProcessResultDTO>();
            foreach (var campaign in activeCampaigns)
            {
                results.Add(await ProcessCampaignAsync(campaign, date));
            }

            return results;
        }

        public async Task<VoucherCampaignProcessResultDTO?> ProcessMilestoneCampaignsAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var date = DateTime.UtcNow.ToVnTime().Date;
            var campaigns = await GetActiveCampaignsQuery(now)
                .Where(v => v.CampaignType == VoucherCampaignType.Milestone)
                .OrderBy(v => v.MilestoneUsageCount)
                .ToListAsync();

            VoucherCampaignProcessResultDTO? lastResult = null;
            foreach (var campaign in campaigns)
            {
                var result = await ProcessCampaignAsync(campaign, date, userId);
                if (result.GrantedCount > 0) lastResult = result;
            }

            return lastResult;
        }

        private IQueryable<Voucher> GetActiveCampaignsQuery(DateTime now)
        {
            return _context.Vouchers
                .Include(v => v.RequiredTier)
                .Where(v => v.IsActive
                         && v.CampaignType != VoucherCampaignType.Manual
                         && v.VoucherType == VoucherType.Discount
                         && (v.StartDate == null || v.StartDate <= now)
                         && v.ExpiryDate >= now
                         && (v.MaxUsages <= 0 || v.CurrentUsageCount < v.MaxUsages));
        }

        private async Task<VoucherCampaignProcessResultDTO> ProcessCampaignAsync(Voucher campaign, DateTime targetDate, int? specificUserId = null)
        {
            var eligibleUsers = await GetEligibleUsersAsync(campaign, targetDate, specificUserId);
            var result = new VoucherCampaignProcessResultDTO
            {
                CampaignType = (AutoWashPro.BLL.Enums.VoucherCampaignTypeEnum)campaign.CampaignType,
                VoucherCode = campaign.Code,
                ScannedUsers = eligibleUsers.Count
            };
            var emailItems = new List<(User User, DateTime ExpiryDate)>();

            foreach (var user in eligibleUsers)
            {
                var triggerKey = BuildTriggerKey(campaign, targetDate);
                var alreadyGranted = await _context.UserVouchers.AnyAsync(uv =>
                    uv.UserId == user.UserId
                    && uv.VoucherId == campaign.VoucherId
                    && uv.TriggerKey == triggerKey);

                if (alreadyGranted)
                {
                    result.SkippedCount++;
                    continue;
                }

                var receivedDate = DateTime.UtcNow;
                var userExpiryDate = CalculateUserVoucherExpiry(campaign, receivedDate);
                var userVoucher = new UserVoucher
                {
                    UserId = user.UserId,
                    VoucherId = campaign.VoucherId,
                    IsUsed = false,
                    UsageCount = 0,
                    ReceivedDate = receivedDate,
                    ExpiryDate = userExpiryDate,
                    TriggerKey = triggerKey
                };

                _context.UserVouchers.Add(userVoucher);

                if (campaign.CampaignType == VoucherCampaignType.Birthday && user.CustomerProfile != null)
                {
                    user.CustomerProfile.LastBirthdayGiftYear = targetDate.Year;
                }
                else if (campaign.CampaignType == VoucherCampaignType.Winback && user.CustomerProfile != null)
                {
                    user.CustomerProfile.LastWinbackSentDate = targetDate;
                }

                result.GrantedCount++;
                result.GrantedUsers.Add(new VoucherCampaignGrantDTO
                {
                    UserId = user.UserId,
                    VoucherId = campaign.VoucherId,
                    VoucherCode = campaign.Code,
                    TriggerKey = triggerKey,
                    ReceivedDate = userVoucher.ReceivedDate,
                    ExpiryDate = userVoucher.ExpiryDate
                });
                emailItems.Add((user, userVoucher.ExpiryDate));
            }

            await _context.SaveChangesAsync();
            foreach (var item in emailItems)
            {
                await SendVoucherEmailAsync(item.User, campaign, item.ExpiryDate);
            }

            result.SkippedCount = result.ScannedUsers - result.GrantedCount;
            return result;
        }

        private async Task SendVoucherEmailAsync(User user, Voucher voucher, DateTime userExpiryDate)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            try
            {
                var customerName = user.CustomerProfile?.FullName ?? "Valued Customer";
                var html = EmailTemplateBuilder.BuildVoucherCampaignEmail(voucher, customerName, userExpiryDate);
                await _emailService.SendEmailAsync(user.Email, $"[SmartWash] New Voucher: {voucher.Code}", html);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Voucher email error] User #{user.UserId}, Voucher #{voucher.VoucherId}: {ex.Message}");
            }
        }

        private async Task<List<User>> GetEligibleUsersAsync(Voucher campaign, DateTime targetDate, int? specificUserId)
        {
            var usersQuery = _context.Users
                .Include(u => u.CustomerProfile)
                .ThenInclude(cp => cp!.Tier)
                .Where(u => u.Status == "Active" && u.CustomerProfile != null);

            if (specificUserId.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.UserId == specificUserId.Value);
            }

            var users = await usersQuery.ToListAsync();
            return campaign.CampaignType switch
            {
                VoucherCampaignType.Birthday => users
                    .Where(u => u.CustomerProfile!.DateOfBirth.HasValue
                             && u.CustomerProfile.DateOfBirth.Value.Month == targetDate.Month
                             && u.CustomerProfile.DateOfBirth.Value.Day == targetDate.Day
                             && (u.CustomerProfile.LastBirthdayGiftYear == null || u.CustomerProfile.LastBirthdayGiftYear < targetDate.Year))
                    .ToList(),

                VoucherCampaignType.Age => users
                    .Where(u => u.CustomerProfile!.DateOfBirth.HasValue
                             && campaign.TargetAge.HasValue
                             && CalculateAge(u.CustomerProfile.DateOfBirth.Value, targetDate) == campaign.TargetAge.Value)
                    .ToList(),

                VoucherCampaignType.Winback => users
                    .Where(u => u.CustomerProfile!.LastVisitDate.HasValue
                             && campaign.InactiveDays.HasValue
                             && u.CustomerProfile.LastVisitDate.Value.Date <= targetDate.AddDays(-campaign.InactiveDays.Value)
                             && (!u.CustomerProfile.LastWinbackSentDate.HasValue
                                 || !campaign.ResendAfterDays.HasValue
                                 || u.CustomerProfile.LastWinbackSentDate.Value.Date <= targetDate.AddDays(-campaign.ResendAfterDays.Value)))
                    .ToList(),

                VoucherCampaignType.Vip => users
                    .Where(u => IsUserTierEligible(u.CustomerProfile!, campaign.RequiredTier))
                    .ToList(),

                VoucherCampaignType.Milestone => await GetMilestoneUsersAsync(users, campaign),

                _ => new List<User>()
            };
        }

        private async Task<List<User>> GetMilestoneUsersAsync(List<User> users, Voucher campaign)
        {
            if (!campaign.MilestoneUsageCount.HasValue) return new List<User>();

            var userIds = users.Select(u => u.UserId).ToList();
            var bookingCounts = await _context.Bookings
                .Where(b => b.UserId.HasValue
                         && userIds.Contains(b.UserId.Value)
                         && b.Status == "Completed")
                .GroupBy(b => b.UserId!.Value)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            return users
                .Where(u => bookingCounts.TryGetValue(u.UserId, out var count)
                         && count >= campaign.MilestoneUsageCount.Value)
                .ToList();
        }

        private async Task<Voucher> CreateCampaignVoucherAsync(CreateAutomatedVoucherBaseDTO request, VoucherCampaignType campaignType, Action<Voucher>? configure = null)
        {
            var code = request.Code.Trim().ToUpperInvariant();
            if (await _context.Vouchers.AnyAsync(v => v.Code == code))
                throw new BadRequestException("Voucher code already exists.");

            if (request.RequiredTierId.HasValue)
            {
                var tierExists = await _context.Tiers.AnyAsync(t => t.TierId == request.RequiredTierId.Value);
                if (!tierExists) throw new BadRequestException("Required tier does not exist.");
            }

            var now = DateTime.UtcNow;
            var startDate = request.StartDate?.ToUniversalTime() ?? now;
            var endDate = request.EndDate?.ToUniversalTime() ?? startDate.AddDays(request.ExpiryDays);
            if (endDate <= now) throw new BadRequestException("Expiration date must be in the future.");

            var voucher = new Voucher
            {
                Code = code,
                DiscountAmount = request.DiscountAmount,
                MaxUsages = request.MaxUsages,
                CurrentUsageCount = 0,
                MaxUsagePerUser = request.MaxUsagePerUser,
                ExpiryDate = endDate,
                ExpiryDays = request.ExpiryDays,
                StartDate = startDate,
                CreatedAt = now,
                IsActive = request.IsActive,
                PointsRequired = 0,
                VoucherType = VoucherType.Discount,
                CampaignType = campaignType,
                ImageUrl = request.ImageUrl,
                MinOrderAmount = request.MinOrderAmount,
                RequiredTierId = request.RequiredTierId,
                ValidStartTime = request.ValidStartTime,
                ValidEndTime = request.ValidEndTime
            };

            configure?.Invoke(voucher);
            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();

            if (voucher.RequiredTierId.HasValue)
            {
                await _context.Entry(voucher).Reference(v => v.RequiredTier).LoadAsync();
            }

            return voucher;
        }

        private static string BuildTriggerKey(Voucher campaign, DateTime targetDate)
        {
            return campaign.CampaignType switch
            {
                VoucherCampaignType.Birthday => $"BIRTHDAY-{targetDate:yyyy}",
                VoucherCampaignType.Age => $"AGE-{campaign.TargetAge}",
                VoucherCampaignType.Winback => $"WINBACK-{targetDate:yyyyMMdd}",
                VoucherCampaignType.Vip => $"VIP-{targetDate:yyyyMM}",
                VoucherCampaignType.Milestone => $"MILESTONE-{campaign.MilestoneUsageCount}",
                _ => $"MANUAL-{targetDate:yyyyMMdd}"
            };
        }

        private static bool CanProcessCampaignNow(Voucher campaign)
        {
            var now = DateTime.UtcNow;
            return campaign.IsActive
                && campaign.VoucherType == VoucherType.Discount
                && (campaign.StartDate == null || campaign.StartDate <= now)
                && campaign.ExpiryDate >= now
                && (campaign.MaxUsages <= 0 || campaign.CurrentUsageCount < campaign.MaxUsages);
        }

        private static int CalculateAge(DateTime dateOfBirth, DateTime targetDate)
        {
            var age = targetDate.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > targetDate.AddYears(-age)) age--;
            return age;
        }

        private static bool IsUserTierEligible(CustomerProfile profile, Tier? requiredTier)
        {
            if (requiredTier == null || profile.Tier == null) return false;
            return profile.Tier.MinAccumulatedPoints >= requiredTier.MinAccumulatedPoints;
        }

        private static CampaignVoucherResponseDTO MapCampaignDto(Voucher v) => new()
        {
            VoucherId = v.VoucherId,
            Code = v.Code,
            DiscountAmount = v.DiscountAmount,
            MaxUsages = v.MaxUsages,
            MaxUsagePerUser = v.MaxUsagePerUser,
            ExpiryDays = v.ExpiryDays,
            StartDate = v.StartDate,
            EndDate = v.ExpiryDate,
            MinOrderAmount = v.MinOrderAmount,
            ImageUrl = v.ImageUrl,
            RequiredTierId = v.RequiredTierId,
            ValidStartTime = v.ValidStartTime,
            ValidEndTime = v.ValidEndTime,
            IsActive = v.IsActive,
            TargetAge = v.TargetAge,
            InactiveDays = v.InactiveDays,
            ResendAfterDays = v.ResendAfterDays,
            MilestoneUsageCount = v.MilestoneUsageCount
        };

        private static DateTime CalculateUserVoucherExpiry(Voucher voucher, DateTime receivedDate)
        {
            var userExpiry = voucher.ExpiryDays.HasValue
                ? receivedDate.AddDays(voucher.ExpiryDays.Value)
                : voucher.ExpiryDate;

            return userExpiry <= voucher.ExpiryDate ? userExpiry : voucher.ExpiryDate;
        }
    }
}
