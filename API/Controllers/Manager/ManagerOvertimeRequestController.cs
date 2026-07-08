using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Manager
{
    [Route("api/v1/manager/overtime-requests")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerOvertimeRequestController : ControllerBase
    {
        private readonly IStaffManagementService _staffService;

        public ManagerOvertimeRequestController(IStaffManagementService staffService)
        {
            _staffService = staffService;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetOvertimeRequests([FromQuery] string? status = null)
        {
            var result = await _staffService.GetOvertimeRequestsAsync(status);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewOvertimeRequest(int id, [FromBody] ReviewRequestDTO request)
        {
            var result = await _staffService.ReviewOvertimeRequestAsync(id, GetUserId(), request);
            return Ok(new { statusCode = 200, message = "Overtime request reviewed successfully.", data = result });
        }
    }
}
