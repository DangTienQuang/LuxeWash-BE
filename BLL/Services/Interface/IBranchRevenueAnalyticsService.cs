using System.Collections.Generic;
using System.Threading.Tasks;
using BLL.DTOs.Business;

namespace BLL.Services.Interface
{
    public interface IBranchRevenueAnalyticsService
    {
        Task<BranchMonthlyRevenueDTO> EvaluateBranchMonthlyRevenueAsync(int branchId, int? targetMonth = null, int? targetYear = null);
        Task<MonthlyRevenueCampaignResultDTO> CheckAndTriggerMonthlyRevenueCampaignAsync(int branchId, int? targetMonth = null, int? targetYear = null);
        Task<List<MonthlyRevenueCampaignResultDTO>> CheckAndTriggerAllBranchesRevenueCampaignAsync(int? targetMonth = null, int? targetYear = null);
        Task<List<VoucherProposalDTO>> GetPendingProposalsAsync(int branchId);
        Task<VoucherProposalDTO> ModifyProposalAsync(int branchId, int voucherId, ModifyVoucherProposalDTO dto);
        Task<MonthlyRevenueCampaignResultDTO> ApproveProposalAsync(int branchId, int voucherId);
        Task<bool> RejectProposalAsync(int branchId, int voucherId, string? rejectReason);
        Task<BranchComprehensiveStimulusDTO> GenerateComprehensiveStimulusAnalysisAsync(int branchId, int? targetMonth = null, int? targetYear = null);
    }
}
