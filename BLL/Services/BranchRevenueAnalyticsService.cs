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
                    Message = $"Doanh thu tháng {eval.TargetMonth:D2}/{eval.TargetYear} của {eval.BranchName} đạt {eval.CurrentMonthRevenue:N0}đ (ổn định hoặc tăng trưởng so với tháng trước {eval.PreviousMonthRevenue:N0}đ). Không phát phiếu giảm giá.",
                    GeneratedVoucherCode = null,
                    DiscountPercentage = 0,
                    GrantedUsersCount = 0
                };
            }

            string voucherCode = $"WINBACK_BR{branchId}_M{eval.TargetMonth:D2}Y{eval.TargetYear}_{eval.CalculatedVoucherDiscountPercent}%";

            // Check if voucher already exists
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode);
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
                    IsActive = true,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(30)
                };

                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
            }

            // Find target customer users (users who have booked at this branch or active customers)
            var branchCustomerIds = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.UserId != null)
                .Select(b => b.UserId!.Value)
                .Distinct()
                .ToListAsync();

            if (!branchCustomerIds.Any())
            {
                // Fallback to all active customer users if branch has no past distinct user bookings recorded
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
                        ExpiryDate = DateTime.UtcNow.AddDays(30),
                        IsUsed = false,
                        TriggerKey = $"RevenueWinback_BR{branchId}_M{eval.TargetMonth:D2}Y{eval.TargetYear}"
                    };

                    _context.UserVouchers.Add(userVoucher);
                    grantedCount++;
                }
            }

            if (grantedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Granted {Count} winback vouchers ({Code}) for branch {BranchId}", grantedCount, voucherCode, branchId);
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
                IsCampaignTriggered = true,
                Message = $"Doanh thu tháng {eval.TargetMonth:D2}/{eval.TargetYear} của {eval.BranchName} giảm {eval.RevenueDropPercentage}% (còn {eval.CurrentMonthRevenue:N0}đ so với {eval.PreviousMonthRevenue:N0}đ). Đã tự động phát hành Voucher giảm {eval.CalculatedVoucherDiscountPercent}% ({voucherCode}) cho {grantedCount} khách hàng.",
                GeneratedVoucherCode = voucherCode,
                DiscountPercentage = eval.CalculatedVoucherDiscountPercent,
                GrantedUsersCount = grantedCount
            };
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
    }
}
