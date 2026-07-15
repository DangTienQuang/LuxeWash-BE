using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
using BLL.DTOs.Business;
using BLL.Helpers;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BLL.Services
{
    public class BranchRevenueAnalyticsService : IBranchRevenueAnalyticsService
    {
        private readonly AutoWashDbContext _context;
        private readonly ILogger<BranchRevenueAnalyticsService> _logger;

        public BranchRevenueAnalyticsService(AutoWashDbContext context, ILogger<BranchRevenueAnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BranchMonthlyRevenueDTO> EvaluateBranchMonthlyRevenueAsync(int branchId, int? targetMonth = null, int? targetYear = null)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
            if (branch == null)
            {
                throw new NotFoundException($"Branch with ID {branchId} not found.");
            }

            var now = DateTime.UtcNow.ToVnTime();
            int month = targetMonth ?? now.Month;
            int year = targetYear ?? now.Year;

            // Compute current target month range
            var currentMonthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var currentMonthEnd = currentMonthStart.AddMonths(1);

            // Compute previous month range
            var prevMonthStart = currentMonthStart.AddMonths(-1);
            var prevMonthEnd = currentMonthStart;

            // Query completed bookings revenue for current month
            var currentRevenue = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.Status == "Completed" && b.ScheduledTime >= currentMonthStart && b.ScheduledTime < currentMonthEnd)
                .SumAsync(b => b.FinalAmount);

            // Query completed bookings revenue for previous month
            var prevRevenue = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.Status == "Completed" && b.ScheduledTime >= prevMonthStart && b.ScheduledTime < prevMonthEnd)
                .SumAsync(b => b.FinalAmount);

            decimal dropAmount = prevRevenue - currentRevenue;
            double dropPercentage = 0;
            bool isDropped = false;
            int discountPercent = 0;

            if (prevRevenue > 0 && dropAmount > 0)
            {
                dropPercentage = (double)(dropAmount / prevRevenue) * 100.0;
                isDropped = true;

                // Formula: min(max(RoundTo5(dropPercentage * 0.8 + 5), 10), 30)
                double rawCalc = dropPercentage * 0.8 + 5.0;
                int roundedTo5 = (int)(Math.Round(rawCalc / 5.0) * 5.0);
                discountPercent = Math.Clamp(roundedTo5, 10, 30);
            }

            return new BranchMonthlyRevenueDTO
            {
                BranchId = branch.BranchId,
                BranchName = branch.Name,
                TargetMonth = month,
                TargetYear = year,
                PreviousMonthRevenue = prevRevenue,
                CurrentMonthRevenue = currentRevenue,
                RevenueDropAmount = dropAmount > 0 ? dropAmount : 0,
                RevenueDropPercentage = Math.Round(dropPercentage, 2),
                IsRevenueDropped = isDropped,
                CalculatedVoucherDiscountPercent = discountPercent
            };
        }

        public async Task<MonthlyRevenueCampaignResultDTO> CheckAndTriggerMonthlyRevenueCampaignAsync(int branchId, int? targetMonth = null, int? targetYear = null)
        {
            var eval = await EvaluateBranchMonthlyRevenueAsync(branchId, targetMonth, targetYear);

            if (!eval.IsRevenueDropped || eval.CalculatedVoucherDiscountPercent <= 0)
            {
                return new MonthlyRevenueCampaignResultDTO
                {
                    BranchId = eval.BranchId,
                    BranchName = eval.BranchName,
                    TargetMonth = eval.TargetMonth,
                    TargetYear = eval.TargetYear,
                    PreviousMonthRevenue = eval.PreviousMonthRevenue,
                    CurrentMonthRevenue = eval.CurrentMonthRevenue,
                    RevenueDropPercentage = eval.RevenueDropPercentage,
                    IsCampaignTriggered = false,
                    ApprovalStatus = "N/A",
                    Message = $"Doanh thu tháng {eval.TargetMonth:D2}/{eval.TargetYear} của {eval.BranchName} đạt {eval.CurrentMonthRevenue:N0}đ (ổn định hoặc tăng trưởng so với tháng trước {eval.PreviousMonthRevenue:N0}đ). Không cần đề xuất phiếu giảm giá.",
                    GeneratedVoucherCode = null,
                    DiscountPercentage = 0,
                    GrantedUsersCount = 0
                };
            }

            string voucherCode = $"WINBACK_BR{branchId}_M{eval.TargetMonth:D2}Y{eval.TargetYear}_{eval.CalculatedVoucherDiscountPercent}%";

            // Check if voucher or proposal already exists
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode && v.BranchId == branchId);
            if (voucher == null)
            {
                voucher = new Voucher
                {
                    Code = voucherCode,
                    DiscountAmount = eval.CalculatedVoucherDiscountPercent,
                    VoucherType = VoucherType.Discount,
                    CampaignType = VoucherCampaignType.Winback,
                    BranchId = branchId,
                    ExpiryDays = 30,
                    MaxUsagePerUser = 1,
                    MaxUsages = 999999,
                    IsActive = false, // Not active yet, waiting for Manager approval
                    ApprovalStatus = "Proposed",
                    ProposalNote = $"Doanh thu tháng {eval.TargetMonth:D2}/{eval.TargetYear} của {eval.BranchName} giảm {eval.RevenueDropPercentage}% (còn {eval.CurrentMonthRevenue:N0}đ so với {eval.PreviousMonthRevenue:N0}đ). Đề xuất Voucher giảm {eval.CalculatedVoucherDiscountPercent}% để kéo khách hàng trở lại.",
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(30)
                };

                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created winback voucher proposal ({Code}) for branch {BranchId}, status: Proposed", voucherCode, branchId);
            }

            // Estimate target customer users (users who have booked at this branch or active customers)
            var branchCustomerIds = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.UserId != null)
                .Select(b => b.UserId!.Value)
                .Distinct()
                .ToListAsync();

            if (!branchCustomerIds.Any())
            {
                branchCustomerIds = await _context.Users
                    .Where(u => u.Status == "Active" && u.Role == "Customer")
                    .Select(u => u.UserId)
                    .ToListAsync();
            }

            // If voucher is already Approved, we count actual granted vouchers
            int grantedCount = 0;
            if (voucher.ApprovalStatus == "Approved")
            {
                grantedCount = await _context.UserVouchers.CountAsync(uv => uv.VoucherId == voucher.VoucherId);
            }

            return new MonthlyRevenueCampaignResultDTO
            {
                BranchId = eval.BranchId,
                BranchName = eval.BranchName,
                TargetMonth = eval.TargetMonth,
                TargetYear = eval.TargetYear,
                PreviousMonthRevenue = eval.PreviousMonthRevenue,
                CurrentMonthRevenue = eval.CurrentMonthRevenue,
                RevenueDropPercentage = eval.RevenueDropPercentage,
                IsCampaignTriggered = voucher.ApprovalStatus == "Approved",
                ApprovalStatus = voucher.ApprovalStatus,
                Message = voucher.ApprovalStatus == "Proposed"
                    ? $"Doanh thu giảm {eval.RevenueDropPercentage}%. Hệ thống đã tạo ĐỀ XUẤT Voucher ({voucherCode}) chờ Manager xét duyệt (Số khách quen mục tiêu: ~{branchCustomerIds.Count} người)."
                    : $"Doanh thu giảm {eval.RevenueDropPercentage}%. Voucher ({voucherCode}) đã được xét duyệt (Trạng thái: {voucher.ApprovalStatus}).",
                GeneratedVoucherCode = voucher.Code,
                DiscountPercentage = (int)voucher.DiscountAmount,
                GrantedUsersCount = grantedCount
            };
        }

        public async Task<List<VoucherProposalDTO>> GetPendingProposalsAsync(int branchId)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
            if (branch == null) throw new NotFoundException($"Branch with ID {branchId} not found.");

            var proposals = await _context.Vouchers
                .Where(v => v.BranchId == branchId && v.ApprovalStatus == "Proposed" && v.CampaignType == VoucherCampaignType.Winback)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            var branchCustomerCount = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.UserId != null)
                .Select(b => b.UserId!.Value)
                .Distinct()
                .CountAsync();

            if (branchCustomerCount == 0)
            {
                branchCustomerCount = await _context.Users.CountAsync(u => u.Status == "Active" && u.Role == "Customer");
            }

            var list = new List<VoucherProposalDTO>();
            var now = DateTime.UtcNow.ToVnTime();

            foreach (var v in proposals)
            {
                list.Add(new VoucherProposalDTO
                {
                    VoucherId = v.VoucherId,
                    Code = v.Code,
                    DiscountAmount = v.DiscountAmount,
                    MaxUsages = v.MaxUsages,
                    ExpiryDays = v.ExpiryDays,
                    ApprovalStatus = v.ApprovalStatus,
                    ProposalNote = v.ProposalNote,
                    BranchId = branchId,
                    BranchName = branch.Name,
                    TargetMonth = now.Month,
                    TargetYear = now.Year,
                    EstimatedTargetCustomers = branchCustomerCount,
                    CreatedAt = v.CreatedAt
                });
            }

            return list;
        }

        public async Task<VoucherProposalDTO> ModifyProposalAsync(int branchId, int voucherId, ModifyVoucherProposalDTO dto)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == voucherId && v.BranchId == branchId);
            if (voucher == null) throw new NotFoundException($"Proposal Voucher #{voucherId} not found for this branch.");

            if (voucher.ApprovalStatus != "Proposed")
            {
                throw new BadRequestException($"Only proposals in 'Proposed' status can be modified. Current status: {voucher.ApprovalStatus}");
            }

            if (!string.IsNullOrWhiteSpace(dto.Code))
            {
                var exists = await _context.Vouchers.AnyAsync(v => v.Code == dto.Code && v.VoucherId != voucherId);
                if (exists) throw new BadRequestException($"Voucher code '{dto.Code}' already exists.");
                voucher.Code = dto.Code.Trim();
            }

            if (dto.DiscountAmount.HasValue && dto.DiscountAmount.Value > 0)
            {
                voucher.DiscountAmount = dto.DiscountAmount.Value;
            }

            if (dto.MaxUsages.HasValue && dto.MaxUsages.Value > 0)
            {
                voucher.MaxUsages = dto.MaxUsages.Value;
            }

            if (dto.ExpiryDays.HasValue && dto.ExpiryDays.Value > 0)
            {
                voucher.ExpiryDays = dto.ExpiryDays.Value;
                voucher.ExpiryDate = DateTime.UtcNow.AddDays(dto.ExpiryDays.Value);
            }

            if (dto.ProposalNote != null)
            {
                voucher.ProposalNote = dto.ProposalNote;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Modified voucher proposal #{VoucherId} for branch {BranchId}", voucherId, branchId);

            var branch = await _context.Branches.FirstAsync(b => b.BranchId == branchId);
            var now = DateTime.UtcNow.ToVnTime();

            return new VoucherProposalDTO
            {
                VoucherId = voucher.VoucherId,
                Code = voucher.Code,
                DiscountAmount = voucher.DiscountAmount,
                MaxUsages = voucher.MaxUsages,
                ExpiryDays = voucher.ExpiryDays,
                ApprovalStatus = voucher.ApprovalStatus,
                ProposalNote = voucher.ProposalNote,
                BranchId = branchId,
                BranchName = branch.Name,
                TargetMonth = now.Month,
                TargetYear = now.Year,
                CreatedAt = voucher.CreatedAt
            };
        }

        public async Task<MonthlyRevenueCampaignResultDTO> ApproveProposalAsync(int branchId, int voucherId)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == voucherId && v.BranchId == branchId);
            if (voucher == null) throw new NotFoundException($"Proposal Voucher #{voucherId} not found for this branch.");

            if (voucher.ApprovalStatus != "Proposed")
            {
                throw new BadRequestException($"Cannot approve proposal #{voucherId}. Current status is '{voucher.ApprovalStatus}'.");
            }

            voucher.ApprovalStatus = "Approved";
            voucher.IsActive = true;
            voucher.StartDate = DateTime.UtcNow;
            voucher.ExpiryDate = DateTime.UtcNow.AddDays(voucher.ExpiryDays ?? 30);

            // Find target customer users (users who have booked at this branch or active customers)
            var branchCustomerIds = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.UserId != null)
                .Select(b => b.UserId!.Value)
                .Distinct()
                .ToListAsync();

            if (!branchCustomerIds.Any())
            {
                branchCustomerIds = await _context.Users
                    .Where(u => u.Status == "Active" && u.Role == "Customer")
                    .Select(u => u.UserId)
                    .ToListAsync();
            }

            // Check who already received this voucher
            var existingUserVouchers = await _context.UserVouchers
                .Where(uv => uv.VoucherId == voucher.VoucherId)
                .Select(uv => uv.UserId)
                .ToListAsync();

            var alreadyReceivedSet = new HashSet<int>(existingUserVouchers);
            int grantedCount = 0;

            foreach (var userId in branchCustomerIds)
            {
                if (alreadyReceivedSet.Add(userId))
                {
                    var userVoucher = new UserVoucher
                    {
                        UserId = userId,
                        VoucherId = voucher.VoucherId,
                        ReceivedDate = DateTime.UtcNow,
                        ExpiryDate = DateTime.UtcNow.AddDays(voucher.ExpiryDays ?? 30),
                        IsUsed = false,
                        TriggerKey = $"RevenueWinback_BR{branchId}_Approved"
                    };

                    _context.UserVouchers.Add(userVoucher);
                    grantedCount++;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Approved winback proposal #{VoucherId} ({Code}) for branch {BranchId}. Distributed to {Count} users.", voucherId, voucher.Code, branchId, grantedCount);

            var branch = await _context.Branches.FirstAsync(b => b.BranchId == branchId);
            var now = DateTime.UtcNow.ToVnTime();

            return new MonthlyRevenueCampaignResultDTO
            {
                BranchId = branchId,
                BranchName = branch.Name,
                TargetMonth = now.Month,
                TargetYear = now.Year,
                PreviousMonthRevenue = 0,
                CurrentMonthRevenue = 0,
                RevenueDropPercentage = 0,
                IsCampaignTriggered = true,
                ApprovalStatus = "Approved",
                Message = $"Đã PHÊ DUYỆT thành công đề xuất Voucher '{voucher.Code}'. Đã phát hành và gửi vào ví của {grantedCount} khách hàng quen thuộc!",
                GeneratedVoucherCode = voucher.Code,
                DiscountPercentage = (int)voucher.DiscountAmount,
                GrantedUsersCount = grantedCount
            };
        }

        public async Task<bool> RejectProposalAsync(int branchId, int voucherId, string? rejectReason)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == voucherId && v.BranchId == branchId);
            if (voucher == null) throw new NotFoundException($"Proposal Voucher #{voucherId} not found for this branch.");

            if (voucher.ApprovalStatus != "Proposed")
            {
                throw new BadRequestException($"Cannot reject proposal #{voucherId}. Current status is '{voucher.ApprovalStatus}'.");
            }

            voucher.ApprovalStatus = "Rejected";
            voucher.IsActive = false;

            if (!string.IsNullOrWhiteSpace(rejectReason))
            {
                voucher.ProposalNote = (voucher.ProposalNote + $" [Từ chối bởi Manager: {rejectReason.Trim()}]").Trim();
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Rejected winback proposal #{VoucherId} for branch {BranchId}. Reason: {Reason}", voucherId, branchId, rejectReason);
            return true;
        }

        public async Task<List<MonthlyRevenueCampaignResultDTO>> CheckAndTriggerAllBranchesRevenueCampaignAsync(int? targetMonth = null, int? targetYear = null)
        {
            var activeBranches = await _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.BranchId)
                .ToListAsync();

            var results = new List<MonthlyRevenueCampaignResultDTO>();
            foreach (var branch in activeBranches)
            {
                var res = await CheckAndTriggerMonthlyRevenueCampaignAsync(branch.BranchId, targetMonth, targetYear);
                results.Add(res);
            }

            return results;
        }

        public async Task<BranchComprehensiveStimulusDTO> GenerateComprehensiveStimulusAnalysisAsync(int branchId, int? targetMonth = null, int? targetYear = null)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) throw new NotFoundException($"Branch with ID {branchId} not found.");

            var now = DateTime.UtcNow;
            int month = targetMonth ?? now.Month;
            int year = targetYear ?? now.Year;

            // 1. Đối chiếu mức doanh thu hiện tại với tháng trước
            var eval = await EvaluateBranchMonthlyRevenueAsync(branchId, month, year);
            bool isHealthy = !eval.IsRevenueDropped || eval.CalculatedVoucherDiscountPercent <= 0;

            // 2. Thống kê lưu lượng khách ra vào trong tháng và xác định khung/ngày vắng khách
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            if (year == now.Year && month == now.Month)
            {
                endDate = now;
            }
            int daysCount = Math.Max(1, (endDate - startDate).Days + 1);

            var bookingsInMonth = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.ScheduledTime >= startDate && b.ScheduledTime <= endDate &&
                            (b.Status == "CheckedIn" || b.Status == "Processing" || b.Status == "Completed"))
                .ToListAsync();

            int totalCheckIns = bookingsInMonth.Count;
            double avgDaily = Math.Round((double)totalCheckIns / daysCount, 1);

            var dayGroups = bookingsInMonth
                .GroupBy(b => b.ScheduledTime.DayOfWeek)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .OrderBy(g => g.Count)
                .ToList();

            string slowestDaysStr = "Thứ 3, Thứ 4";
            if (dayGroups.Any())
            {
                var names = dayGroups.Take(2).Select(g => GetVietnameseDayOfWeek(g.Day)).ToList();
                slowestDaysStr = string.Join(", ", names);
            }

            // 3. Thống kê khách hàng thân thiết (>2 lượt) và số lượng có dấu hiệu rời bỏ (>45 ngày)
            var fortyFiveDaysAgo = DateTime.UtcNow.AddDays(-45);
            var loyalCustomers = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.UserId != null && b.Status == "Completed")
                .GroupBy(b => b.UserId!.Value)
                .Where(g => g.Count() >= 2)
                .Select(g => new { UserId = g.Key, LastVisit = g.Max(b => b.ScheduledTime) })
                .ToListAsync();

            int activeCount = loyalCustomers.Count;
            int atRiskCount = loyalCustomers.Count(c => c.LastVisit < fortyFiveDaysAgo);

            // 4. Tính toán mức giảm giá và đề xuất kịch bản dựa trên đối chiếu doanh thu
            int weekdayDiscount = 15;
            int winbackDiscount = 20;

            if (eval.IsRevenueDropped)
            {
                if (eval.RevenueDropPercentage >= 25)
                {
                    weekdayDiscount = 20;
                    winbackDiscount = 25;
                }
                else if (eval.RevenueDropPercentage >= 10)
                {
                    weekdayDiscount = 15;
                    winbackDiscount = 20;
                }
                else
                {
                    weekdayDiscount = 10;
                    winbackDiscount = 15;
                }
            }
            else
            {
                weekdayDiscount = 10;
                winbackDiscount = 15;
            }

            var proposalsList = new List<VoucherProposalDTO>();

            // Kịch bản 1: Ngày trong tuần vắng khách
            string weekdayCode = $"OFFPEAK_B{branchId}_M{month:D2}Y{year}_{weekdayDiscount}";
            var existingWeekdayVoucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.BranchId == branchId && v.Code == weekdayCode);

            if (existingWeekdayVoucher == null)
            {
                existingWeekdayVoucher = new Voucher
                {
                    Code = weekdayCode,
                    DiscountAmount = weekdayDiscount,
                    MaxUsages = 100,
                    CurrentUsageCount = 0,
                    MaxUsagePerUser = 2,
                    ExpiryDays = 30,
                    ExpiryDate = DateTime.UtcNow.AddDays(30),
                    IsActive = false,
                    PointsRequired = 0,
                    VoucherType = VoucherType.Discount,
                    CampaignType = VoucherCampaignType.Manual,
                    BranchId = branchId,
                    ApprovalStatus = "Proposed",
                    ProposalNote = $"[Phân tích AI] Đối chiếu doanh thu tháng {month:D2}/{year} {(eval.IsRevenueDropped ? $"sụt giảm {eval.RevenueDropPercentage}%" : "ổn định")}, phát hiện lưu lượng vào {slowestDaysStr} rất thấp (trung bình ~{avgDaily} xe/ngày). Đề xuất giảm {weekdayDiscount}% cho các ngày vắng khách nhằm tối ưu công suất xưởng."
                };
                _context.Vouchers.Add(existingWeekdayVoucher);
                await _context.SaveChangesAsync();
            }

            proposalsList.Add(new VoucherProposalDTO
            {
                VoucherId = existingWeekdayVoucher.VoucherId,
                Code = existingWeekdayVoucher.Code,
                DiscountAmount = existingWeekdayVoucher.DiscountAmount,
                MaxUsages = existingWeekdayVoucher.MaxUsages,
                ExpiryDays = existingWeekdayVoucher.ExpiryDays,
                ApprovalStatus = existingWeekdayVoucher.ApprovalStatus,
                ProposalNote = existingWeekdayVoucher.ProposalNote,
                BranchId = branchId,
                BranchName = branch.Name,
                TargetMonth = month,
                TargetYear = year,
                PreviousMonthRevenue = eval.PreviousMonthRevenue,
                CurrentMonthRevenue = eval.CurrentMonthRevenue,
                RevenueDropPercentage = eval.RevenueDropPercentage,
                EstimatedTargetCustomers = totalCheckIns > 0 ? totalCheckIns * 2 : 50,
                CreatedAt = existingWeekdayVoucher.CreatedAt
            });

            // Kịch bản 2: Khách hàng thân thiết có dấu hiệu rời bỏ
            int targetWinbackUsers = Math.Max(10, atRiskCount);
            string winbackCode = $"LOYAL_B{branchId}_M{month:D2}Y{year}_{winbackDiscount}";
            var existingWinbackVoucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.BranchId == branchId && v.Code == winbackCode);

            if (existingWinbackVoucher == null)
            {
                existingWinbackVoucher = new Voucher
                {
                    Code = winbackCode,
                    DiscountAmount = winbackDiscount,
                    MaxUsages = targetWinbackUsers * 2,
                    CurrentUsageCount = 0,
                    MaxUsagePerUser = 1,
                    ExpiryDays = 20,
                    ExpiryDate = DateTime.UtcNow.AddDays(20),
                    IsActive = false,
                    PointsRequired = 0,
                    VoucherType = VoucherType.Discount,
                    CampaignType = VoucherCampaignType.Winback,
                    BranchId = branchId,
                    ApprovalStatus = "Proposed",
                    ProposalNote = $"[Phân tích AI] Đối chiếu doanh thu {(eval.IsRevenueDropped ? $"giảm {eval.RevenueDropPercentage}%" : "hiện tại")}, hệ thống phát hiện có {atRiskCount} khách hàng thân thiết (>2 lượt) đã hơn 45 ngày chưa quay lại. Đề xuất ưu đãi {winbackDiscount}% để tri ân và kéo nhóm VIP quay lại ngay."
                };
                _context.Vouchers.Add(existingWinbackVoucher);
                await _context.SaveChangesAsync();
            }

            proposalsList.Add(new VoucherProposalDTO
            {
                VoucherId = existingWinbackVoucher.VoucherId,
                Code = existingWinbackVoucher.Code,
                DiscountAmount = existingWinbackVoucher.DiscountAmount,
                MaxUsages = existingWinbackVoucher.MaxUsages,
                ExpiryDays = existingWinbackVoucher.ExpiryDays,
                ApprovalStatus = existingWinbackVoucher.ApprovalStatus,
                ProposalNote = existingWinbackVoucher.ProposalNote,
                BranchId = branchId,
                BranchName = branch.Name,
                TargetMonth = month,
                TargetYear = year,
                PreviousMonthRevenue = eval.PreviousMonthRevenue,
                CurrentMonthRevenue = eval.CurrentMonthRevenue,
                RevenueDropPercentage = eval.RevenueDropPercentage,
                EstimatedTargetCustomers = targetWinbackUsers,
                CreatedAt = existingWinbackVoucher.CreatedAt
            });

            string summary = $"Doanh thu tháng {month:D2}/{year} đạt {eval.CurrentMonthRevenue:N0}đ " +
                             $"{(eval.IsRevenueDropped ? $"(giảm {eval.RevenueDropPercentage}% so với tháng trước {eval.PreviousMonthRevenue:N0}đ)" : $"(duy trì/tăng so với {eval.PreviousMonthRevenue:N0}đ)")}. " +
                             $"Lưu lượng khách trung bình {avgDaily} xe/ngày, vắng nhất vào {slowestDaysStr}. " +
                             $"Phát hiện {atRiskCount} khách hàng thân thiết (>45 ngày chưa quay lại). Hệ thống đã tạo 2 đề xuất mã ưu đãi tối ưu hóa theo tỷ lệ doanh thu hiện tại.";

            return new BranchComprehensiveStimulusDTO
            {
                BranchId = branchId,
                BranchName = branch.Name,
                TargetMonth = month,
                TargetYear = year,
                PreviousMonthRevenue = eval.PreviousMonthRevenue,
                CurrentMonthRevenue = eval.CurrentMonthRevenue,
                RevenueDropPercentage = eval.RevenueDropPercentage,
                IsRevenueHealthy = isHealthy,
                TrafficAndCustomerStats = new BranchTrafficStatsDTO
                {
                    TotalCheckInsThisMonth = totalCheckIns,
                    AverageDailyCheckIns = avgDaily,
                    SlowestDaysOfWeek = slowestDaysStr,
                    AtRiskLoyalCustomersCount = atRiskCount,
                    ActiveCustomersCount = activeCount
                },
                ProposedVouchers = proposalsList,
                ComprehensiveAnalysisSummary = summary
            };
        }

        private static string GetVietnameseDayOfWeek(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "Thứ 2",
                DayOfWeek.Tuesday => "Thứ 3",
                DayOfWeek.Wednesday => "Thứ 4",
                DayOfWeek.Thursday => "Thứ 5",
                DayOfWeek.Friday => "Thứ 6",
                DayOfWeek.Saturday => "Thứ 7",
                DayOfWeek.Sunday => "Chủ Nhật",
                _ => day.ToString()
            };
        }
    }
}
