using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Staff
{
    [Route("api/v1/staff/me")]
    [ApiController]
    [Authorize(Roles = "Staff,Manager")]
    public class StaffSelfServiceController : ControllerBase
    {
        private readonly IStaffManagementService _staffService;

        public StaffSelfServiceController(IStaffManagementService staffService)
        {
            _staffService = staffService;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        [HttpGet("shifts")]
        public async Task<IActionResult> GetMyShifts([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var result = await _staffService.GetMyShiftAssignmentsAsync(GetUserId(), fromDate, toDate);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("overtime-requests")]
        public async Task<IActionResult> GetMyOvertimeRequests()
        {
            var result = await _staffService.GetMyOvertimeRequestsAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("overtime-requests")]
        public async Task<IActionResult> CreateOvertimeRequest([FromBody] CreateOvertimeRequestDTO request)
        {
            var result = await _staffService.CreateOvertimeRequestAsync(GetUserId(), request);
            return Created("", new { statusCode = 201, message = "Overtime request submitted successfully.", data = result });
        }

        [HttpGet("shift-swap-requests")]
        public async Task<IActionResult> GetMyShiftSwapRequests()
        {
            var result = await _staffService.GetMyShiftSwapRequestsAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("shift-swap-requests")]
        public async Task<IActionResult> CreateShiftSwapRequest([FromBody] CreateShiftSwapRequestDTO request)
        {
            var result = await _staffService.CreateShiftSwapRequestAsync(GetUserId(), request);
            return Created("", new { statusCode = 201, message = "Shift swap request submitted successfully.", data = result });
        }
    }
}
