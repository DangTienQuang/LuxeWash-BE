using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Manager
{
    [Route("api/v1/manager/shift-swap-requests")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerShiftSwapRequestController : ControllerBase
    {
        private readonly IStaffManagementService _staffService;

        public ManagerShiftSwapRequestController(IStaffManagementService staffService)
        {
            _staffService = staffService;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetShiftSwapRequests([FromQuery] string? status = null)
        {
            var result = await _staffService.GetShiftSwapRequestsAsync(status);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("{id}/review")]
        public async Task<IActionResult> ReviewShiftSwapRequest(int id, [FromBody] ReviewRequestDTO request)
        {
            var result = await _staffService.ReviewShiftSwapRequestAsync(id, GetUserId(), request);
            return Ok(new { statusCode = 200, message = "Shift swap request reviewed successfully.", data = result });
        }
    }
}
