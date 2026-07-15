using System;
using System.Collections.Generic;

namespace BLL.DTOs.Business
{
    public class BranchMonthlyRevenueDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int TargetMonth { get; set; }
        public int TargetYear { get; set; }
        public decimal PreviousMonthRevenue { get; set; }
        public decimal CurrentMonthRevenue { get; set; }
        public decimal RevenueDropAmount { get; set; }
        public double RevenueDropPercentage { get; set; }
        public bool IsRevenueDropped { get; set; }
        public int CalculatedVoucherDiscountPercent { get; set; }
    }

    public class MonthlyRevenueCampaignResultDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int TargetMonth { get; set; }
        public int TargetYear { get; set; }
        public decimal PreviousMonthRevenue { get; set; }
        public decimal CurrentMonthRevenue { get; set; }
        public double RevenueDropPercentage { get; set; }
        public bool IsCampaignTriggered { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? GeneratedVoucherCode { get; set; }
        public int DiscountPercentage { get; set; }
        public int GrantedUsersCount { get; set; }
    }
}
