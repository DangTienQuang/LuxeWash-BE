using System.Collections.Generic;
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

        public LaneDisplayController(ILaneDisplayPublisherService publisherService)
        {
            _publisherService = publisherService;
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestState(int branchId)
        {
            // Here you might optionally verify if the user has access to this branchId
            // However, the requirement primarily emphasized getting the latest state.

            var states = await _publisherService.GetLatestStateAsync(branchId);
            return Ok(new { StatusCode = 200, Message = "Success", Data = states });
        }
    }
}
