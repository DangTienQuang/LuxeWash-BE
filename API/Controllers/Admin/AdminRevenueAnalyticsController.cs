using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/revenue-analytics")]
    [Authorize(Roles = "Admin")]
    public class AdminRevenueAnalyticsController : ControllerBase
    {
        private readonly IBranchRevenueAnalyticsService _revenueAnalyticsService;

        public AdminRevenueAnalyticsController(IBranchRevenueAnalyticsService revenueAnalyticsService)
        {
            _revenueAnalyticsService = revenueAnalyticsService;
        }

        [HttpGet("evaluate-branch/{branchId}")]
        public async Task<IActionResult> EvaluateBranchRevenue(int branchId, [FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var result = await _revenueAnalyticsService.EvaluateBranchMonthlyRevenueAsync(branchId, month, year);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("trigger-campaign/{branchId}")]
        public async Task<IActionResult> TriggerBranchCampaign(int branchId, [FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var result = await _revenueAnalyticsService.CheckAndTriggerMonthlyRevenueCampaignAsync(branchId, month, year);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("trigger-all-campaigns")]
        public async Task<IActionResult> TriggerAllBranchesCampaign([FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var results = await _revenueAnalyticsService.CheckAndTriggerAllBranchesRevenueCampaignAsync(month, year);
            return Ok(new { statusCode = 200, message = "Success", data = results });
        }
    }
}
