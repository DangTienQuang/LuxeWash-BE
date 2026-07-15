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
    }
}
