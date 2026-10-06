using BLL.Services.Interface;
using BLL.DTOs.Business;
using AutoWashPro.BLL.Services;
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
        private readonly ICRMCampaignService _crmCampaignService;

        public AdminRevenueAnalyticsController(IBranchRevenueAnalyticsService revenueAnalyticsService, ICRMCampaignService crmCampaignService)
        {
            _revenueAnalyticsService = revenueAnalyticsService;
            _crmCampaignService = crmCampaignService;
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

        [HttpPost("comprehensive-proposals/{branchId}")]
        public async Task<IActionResult> GenerateComprehensiveProposals(int branchId, [FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var result = await _revenueAnalyticsService.GenerateComprehensiveStimulusAnalysisAsync(branchId, month, year);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("proposals/{branchId}")]
        public async Task<IActionResult> GetPendingProposals(int branchId)
        {
            var result = await _revenueAnalyticsService.GetPendingProposalsAsync(branchId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("proposals/{branchId}/{voucherId}")]
        public async Task<IActionResult> ModifyProposal(int branchId, int voucherId, [FromBody] ModifyVoucherProposalDTO request)
        {
            var result = await _revenueAnalyticsService.ModifyProposalAsync(branchId, voucherId, request);
            return Ok(new { statusCode = 200, message = "Proposal modified successfully.", data = result });
        }

        [HttpPost("proposals/{branchId}/{voucherId}/approve")]
        public async Task<IActionResult> ApproveProposal(int branchId, int voucherId)
        {
            var result = await _revenueAnalyticsService.ApproveProposalAsync(branchId, voucherId);
            return Ok(new { statusCode = 200, message = "Proposal approved and distributed successfully.", data = result });
        }

        [HttpPost("proposals/{branchId}/{voucherId}/reject")]
        public async Task<IActionResult> RejectProposal(int branchId, int voucherId, [FromBody] RejectVoucherProposalDTO? request)
        {
            var result = await _revenueAnalyticsService.RejectProposalAsync(branchId, voucherId, request?.RejectReason);
            return Ok(new { statusCode = 200, message = "Proposal rejected successfully.", data = result });
        }

        [HttpPost("trigger-weather")]
        public async Task<IActionResult> TriggerWeatherCampaign()
        {
            var result = await _crmCampaignService.TriggerWeatherCampaignAsync();
            return Ok(new { statusCode = 200, message = result });
        }
    }
}
