using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Services.Operations;

namespace AutoWashPro.API.Controllers.Operations
{
    [ApiController]
    [Route("api/v1/operations/branches/{branchId}/lane-display")]
    [Authorize]
    public class LaneDisplayController : ControllerBase
    {
        private readonly ILaneDisplayPublisherService _publisherService;
        private readonly IOperationsMonitoringService _monitoringService;

        public LaneDisplayController(
            ILaneDisplayPublisherService publisherService,
            IOperationsMonitoringService monitoringService)
        {
            _publisherService = publisherService;
            _monitoringService = monitoringService;
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestState(int branchId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var isAuthorized = await _monitoringService.IsEmployeeInBranchAsync(userId, branchId);
            if (!isAuthorized)
                return Forbid();

            var states = await _publisherService.GetLatestStateAsync(branchId);
            return Ok(new { StatusCode = 200, Message = "Success", Data = states });
        }
    }
}
