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
        private readonly AutoWashPro.DAL.Data.AutoWashDbContext _context;

        public LaneDisplayController(ILaneDisplayPublisherService publisherService, AutoWashPro.DAL.Data.AutoWashDbContext context)
        {
            _publisherService = publisherService;
            _context = context;
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestState(int branchId)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var employeeProfile = await _context.EmployeeProfiles.FindAsync(userId);
                if (employeeProfile == null || employeeProfile.BranchId != branchId)
                {
                    return Forbid();
                }
            }
            else
            {
                return Unauthorized();
            }

            var states = await _publisherService.GetLatestStateAsync(branchId);
            return Ok(new { StatusCode = 200, Message = "Success", Data = states });
        }
    }
}
