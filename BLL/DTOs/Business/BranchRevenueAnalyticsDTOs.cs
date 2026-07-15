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
        public string ApprovalStatus { get; set; } = "Approved"; // "Approved", "Proposed", "Rejected"
    }

    public class VoucherProposalDTO
    {
        public int VoucherId { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public int MaxUsages { get; set; }
        public int? ExpiryDays { get; set; }
        public string ApprovalStatus { get; set; } = "Proposed";
        public string? ProposalNote { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int TargetMonth { get; set; }
        public int TargetYear { get; set; }
        public decimal PreviousMonthRevenue { get; set; }
        public decimal CurrentMonthRevenue { get; set; }
        public double RevenueDropPercentage { get; set; }
        public int EstimatedTargetCustomers { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ModifyVoucherProposalDTO
    {
        public string? Code { get; set; }
        public decimal? DiscountAmount { get; set; }
        public int? MaxUsages { get; set; }
        public int? ExpiryDays { get; set; }
        public string? ProposalNote { get; set; }
    }

    public class RejectVoucherProposalDTO
    {
        public string? RejectReason { get; set; }
    }

    public class BranchTrafficStatsDTO
    {
        public int TotalCheckInsThisMonth { get; set; }
        public double AverageDailyCheckIns { get; set; }
        public string SlowestDaysOfWeek { get; set; } = string.Empty;
        public int AtRiskLoyalCustomersCount { get; set; }
        public int ActiveCustomersCount { get; set; }
    }

    public class BranchComprehensiveStimulusDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int TargetMonth { get; set; }
        public int TargetYear { get; set; }
        public decimal PreviousMonthRevenue { get; set; }
        public decimal CurrentMonthRevenue { get; set; }
        public double RevenueDropPercentage { get; set; }
        public bool IsRevenueHealthy { get; set; }
        public BranchTrafficStatsDTO TrafficAndCustomerStats { get; set; } = new();
        public List<VoucherProposalDTO> ProposedVouchers { get; set; } = new();
        public string ComprehensiveAnalysisSummary { get; set; } = string.Empty;
    }
}
