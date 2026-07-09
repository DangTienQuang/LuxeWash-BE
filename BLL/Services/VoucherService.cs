using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class VoucherService : IVoucherService
    {
        private readonly AutoWashDbContext _context;
        private readonly IWalletService _walletService;

        public VoucherService(AutoWashDbContext context, IWalletService walletService)
        {
            _context = context;
            _walletService = walletService;
        }

        public async Task<List<VoucherResponseDTO>> GetMyVouchersAsync(int userId)
        {
            return await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId)
                .Select(uv => new VoucherResponseDTO
                {
                    VoucherId = uv.VoucherId,
                    Code = uv.Voucher.Code,
                    DiscountAmount = uv.Voucher.DiscountAmount,
                    PointsRequired = uv.Voucher.PointsRequired,
                    ExpiryDate = uv.ExpiryDate,
                    CampaignExpiryDate = uv.Voucher.ExpiryDate,
                    ReceivedDate = uv.ReceivedDate,
                    IsUsed = uv.UsageCount >= uv.Voucher.MaxUsagePerUser,
                    UsedDate = uv.UsedDate,
                    UsageCount = uv.UsageCount,
                    MaxUsagePerUser = uv.Voucher.MaxUsagePerUser,
                    RemainingUsage = Math.Max(uv.Voucher.MaxUsagePerUser - uv.UsageCount, 0),
                    MinOrderAmount = uv.Voucher.MinOrderAmount,
                    IsActive = uv.Voucher.IsActive,
                    CampaignType = (AutoWashPro.BLL.Enums.VoucherCampaignTypeEnum)uv.Voucher.CampaignType,
                    VoucherType = (AutoWashPro.BLL.Enums.VoucherTypeEnum)uv.Voucher.VoucherType,
                    ImageUrl = uv.Voucher.ImageUrl,
                    RequiredTierId = uv.Voucher.RequiredTierId,
                    RequiredTierName = uv.Voucher.RequiredTier != null ? uv.Voucher.RequiredTier.TierName : null,
                    ValidStartTime = uv.Voucher.ValidStartTime,
                    ValidEndTime = uv.Voucher.ValidEndTime, VehicleTypeId = uv.Voucher.VehicleTypeId
                })
                .ToListAsync();
        }

        public async Task RedeemVoucherAsync(int userId, int voucherId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var voucher = await _context.Vouchers.Include(v => v.RequiredTier).FirstOrDefaultAsync(v => v.VoucherId == voucherId);
                if (voucher == null) throw new NotFoundException("Voucher does not exist.");
                ValidateVoucherAvailability(voucher);

                if (voucher.RequiredTierId.HasValue)
                {
                    var userProfile = await _context.CustomerProfiles.Include(cp => cp.Tier).FirstOrDefaultAsync(cp => cp.UserId == userId);
                    if (userProfile == null) throw new NotFoundException("User profile not found.");

                    var requiredTier = await _context.Tiers.FindAsync(voucher.RequiredTierId.Value);
                    if (requiredTier != null && userProfile.Tier != null
                        && userProfile.Tier.MinAccumulatedPoints < requiredTier.MinAccumulatedPoints)
                    {
                        throw new BadRequestException($"You need to reach tier {voucher.RequiredTier?.TierName} to redeem this voucher.");
                    }
                }

                var existingUserVoucher = await _context.UserVouchers
                    .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == voucherId && uv.TriggerKey == null);
                if (existingUserVoucher != null) throw new BadRequestException("You already own this voucher.");

                if (voucher.PointsRequired > 0)
                {
                    await _walletService.DeductSpendablePointsAsync(userId, voucher.PointsRequired, $"Redeem voucher: {voucher.Code}");
                }

                _context.UserVouchers.Add(new UserVoucher
                {
                    UserId = userId,
                    VoucherId = voucherId,
                    IsUsed = false,
                    UsageCount = 0,
                    ReceivedDate = DateTime.UtcNow,
                    ExpiryDate = CalculateUserVoucherExpiry(voucher, DateTime.UtcNow)
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                throw new BadRequestException("You already own this voucher; please do not request too rapidly.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<AdminVoucherDTO>> GetAllVouchersAsync()
        {
            var vouchers = await _context.Vouchers.Include(v => v.RequiredTier).OrderByDescending(v => v.ExpiryDate).ToListAsync();
            var redeemCounts = await _context.UserVouchers
                .GroupBy(uv => uv.VoucherId)
                .Select(g => new { VoucherId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VoucherId, x => x.Count);

            return vouchers.Select(v => MapAdminDto(v, redeemCounts.GetValueOrDefault(v.VoucherId, 0))).ToList();
        }

        public async Task GrantVouchersAsync(int voucherId, List<int> userIds)
        {
            if (userIds == null || !userIds.Any()) return;

            var voucher = await _context.Vouchers.FindAsync(voucherId);
            if (voucher == null) throw new NotFoundException("Voucher does not exist.");

            var existingUserIds = await _context.UserVouchers
                .Where(uv => uv.VoucherId == voucherId && userIds.Contains(uv.UserId) && uv.TriggerKey == null)
                .Select(uv => uv.UserId)
                .ToListAsync();

            var userIdsToGrant = userIds.Except(existingUserIds).Distinct().ToList();
            if (!userIdsToGrant.Any()) return;

            var userVouchers = userIdsToGrant.Select(userId => new UserVoucher
            {
                UserId = userId,
                VoucherId = voucherId,
                IsUsed = false,
                UsageCount = 0,
                ReceivedDate = DateTime.UtcNow,
                ExpiryDate = CalculateUserVoucherExpiry(voucher, DateTime.UtcNow)
            });

            _context.UserVouchers.AddRange(userVouchers);
            await _context.SaveChangesAsync();
        }

        public async Task<AdminVoucherDTO> CreateVoucherAsync(CreateOrUpdateVoucherDTO request)
        {
            if (request.ExpiryDate.ToUniversalTime() <= DateTime.UtcNow)
                throw new BadRequestException("Expiration date must be in the future.");

            var code = request.Code.Trim().ToUpperInvariant();
            if (await _context.Vouchers.AnyAsync(v => v.Code == code))
                throw new BadRequestException("Voucher code already exists.");

            await ValidateTierAsync(request.RequiredTierId);

            var voucher = new Voucher
            {
                Code = code,
                DiscountAmount = request.DiscountAmount,
                MaxUsages = request.MaxUsages,
                CurrentUsageCount = 0,
                MaxUsagePerUser = request.MaxUsagePerUser,
                ExpiryDate = request.ExpiryDate.ToUniversalTime(),
                StartDate = request.StartDate?.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow,
                PointsRequired = request.PointsRequired,
                VoucherType = (AutoWashPro.DAL.Enums.VoucherType)request.VoucherType,
                CampaignType = VoucherCampaignType.Manual,
                ImageUrl = request.ImageUrl,
                MinOrderAmount = request.MinOrderAmount,
                IsActive = request.IsActive,
                RequiredTierId = request.RequiredTierId,
                ValidStartTime = request.ValidStartTime,
                ValidEndTime = request.ValidEndTime, VehicleTypeId = request.VehicleTypeId
            };

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();
            await LoadTierAsync(voucher);

            return MapAdminDto(voucher, 0);
        }

        public async Task<AdminVoucherDTO> UpdateVoucherAsync(int id, CreateOrUpdateVoucherDTO request)
        {
            if (request.ExpiryDate.ToUniversalTime() <= DateTime.UtcNow)
                throw new BadRequestException("Expiration date must be in the future.");

            var voucher = await _context.Vouchers.Include(v => v.RequiredTier).FirstOrDefaultAsync(v => v.VoucherId == id);
            if (voucher == null) throw new NotFoundException("Voucher not found.");

            var code = request.Code.Trim().ToUpperInvariant();
            if (await _context.Vouchers.AnyAsync(v => v.Code == code && v.VoucherId != id))
                throw new BadRequestException("Voucher code already exists.");

            await ValidateTierAsync(request.RequiredTierId);

            voucher.Code = code;
            voucher.DiscountAmount = request.DiscountAmount;
            voucher.MaxUsages = request.MaxUsages;
            voucher.MaxUsagePerUser = request.MaxUsagePerUser;
            voucher.ExpiryDate = request.ExpiryDate.ToUniversalTime();
            voucher.StartDate = request.StartDate?.ToUniversalTime();
            voucher.PointsRequired = request.PointsRequired;
            voucher.VoucherType = (AutoWashPro.DAL.Enums.VoucherType)request.VoucherType;
            voucher.CampaignType = VoucherCampaignType.Manual;
            voucher.ImageUrl = request.ImageUrl;
            voucher.MinOrderAmount = request.MinOrderAmount;
            voucher.IsActive = request.IsActive;
            voucher.RequiredTierId = request.RequiredTierId;
            voucher.ValidStartTime = request.ValidStartTime;
            voucher.ValidEndTime = request.ValidEndTime;
            voucher.VehicleTypeId = request.VehicleTypeId;

            await _context.SaveChangesAsync();

            var redeemCount = await _context.UserVouchers.CountAsync(uv => uv.VoucherId == id);
            return MapAdminDto(voucher, redeemCount);
        }

        public async Task<bool> DeleteVoucherAsync(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) throw new NotFoundException("Voucher not found.");

            var hasOwners = await _context.UserVouchers.AnyAsync(uv => uv.VoucherId == id);
            if (hasOwners)
                throw new BadRequestException("Cannot delete voucher that customers have claimed. Please deactivate it or let it expire.");

            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task GenerateCompensationVoucherAsync(int userId)
        {
            var code = $"SORRY-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()}";

            var voucher = new Voucher
            {
                Code = code,
                DiscountAmount = 30000,
                MaxUsages = 1,
                CurrentUsageCount = 0,
                MaxUsagePerUser = 1,
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                PointsRequired = 0,
                IsActive = true
            };

            _context.Vouchers.Add(voucher);
            await _context.SaveChangesAsync();

            _context.UserVouchers.Add(new UserVoucher
            {
                UserId = userId,
                VoucherId = voucher.VoucherId,
                IsUsed = false,
                UsageCount = 0,
                ReceivedDate = DateTime.UtcNow,
                ExpiryDate = voucher.ExpiryDate
            });

            await _context.SaveChangesAsync();
        }

        public async Task<bool> ConsumePhysicalVoucherAsync(int userId, string voucherCode)
        {
            var userVoucher = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Voucher.Code == voucherCode.Trim());

            if (userVoucher == null) throw new NotFoundException("Voucher code does not exist or customer has not claimed it.");
            if (userVoucher.Voucher.VoucherType != VoucherType.PhysicalGift)
                throw new BadRequestException("This voucher is not a physical gift voucher.");

            ValidateVoucherAvailability(userVoucher.Voucher);
            if (userVoucher.ExpiryDate < DateTime.UtcNow) throw new BadRequestException("This voucher has expired.");
            if (userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser)
                throw new BadRequestException("This voucher code has run out of usage limit.");

            userVoucher.UsageCount += 1;
            userVoucher.IsUsed = userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser;
            userVoucher.UsedDate = DateTime.UtcNow;
            userVoucher.LastUsedDate = DateTime.UtcNow;
            userVoucher.Voucher.CurrentUsageCount += 1;

            await _context.SaveChangesAsync();
            return true;
        }

        private static void ValidateVoucherAvailability(Voucher voucher)
        {
            if (!voucher.IsActive) throw new BadRequestException("Voucher is not activated.");
            if (voucher.StartDate.HasValue && voucher.StartDate.Value > DateTime.UtcNow) throw new BadRequestException("Voucher is not yet valid.");
            if (voucher.ExpiryDate < DateTime.UtcNow) throw new BadRequestException("Voucher has expired.");
            if (voucher.MaxUsages > 0 && voucher.CurrentUsageCount >= voucher.MaxUsages) throw new BadRequestException("Voucher usage limit has been reached.");
        }

        private static DateTime CalculateUserVoucherExpiry(Voucher voucher, DateTime receivedDate)
        {
            var userExpiry = voucher.ExpiryDays.HasValue
                ? receivedDate.AddDays(voucher.ExpiryDays.Value)
                : voucher.ExpiryDate;

            return userExpiry <= voucher.ExpiryDate ? userExpiry : voucher.ExpiryDate;
        }

        private async Task ValidateTierAsync(int? tierId)
        {
            if (!tierId.HasValue) return;
            var tierExists = await _context.Tiers.AnyAsync(t => t.TierId == tierId.Value);
            if (!tierExists) throw new BadRequestException("Required tier does not exist.");
        }

        private async Task LoadTierAsync(Voucher voucher)
        {
            if (voucher.RequiredTierId.HasValue)
            {
                await _context.Entry(voucher).Reference(v => v.RequiredTier).LoadAsync();
            }
        }

        private static AdminVoucherDTO MapAdminDto(Voucher v, int redeemedCount) => new()
        {
            VoucherId = v.VoucherId,
            Code = v.Code,
            DiscountAmount = v.DiscountAmount,
            MaxUsages = v.MaxUsages,
            CurrentUsageCount = v.CurrentUsageCount,
            MaxUsagePerUser = v.MaxUsagePerUser,
            ExpiryDate = v.ExpiryDate,
            ExpiryDays = v.ExpiryDays,
            StartDate = v.StartDate,
            PointsRequired = v.PointsRequired,
            RedeemedCount = redeemedCount,
            VoucherType = (AutoWashPro.BLL.Enums.VoucherTypeEnum)v.VoucherType,
            CampaignType = (AutoWashPro.BLL.Enums.VoucherCampaignTypeEnum)v.CampaignType,
            ImageUrl = v.ImageUrl,
            MinOrderAmount = v.MinOrderAmount,
            IsActive = v.IsActive,
            RequiredTierId = v.RequiredTierId,
            RequiredTierName = v.RequiredTier?.TierName,
            ValidStartTime = v.ValidStartTime,
            ValidEndTime = v.ValidEndTime, VehicleTypeId = v.VehicleTypeId
        };
    }
}
