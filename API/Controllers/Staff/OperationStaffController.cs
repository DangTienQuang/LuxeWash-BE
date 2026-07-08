using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.Staff
{
    [ApiController]
    [Route("api/v1/operation-staff")]
    [Authorize(Roles = "Staff")]
    public class OperationStaffController : ControllerBase
    {
        private readonly IOperationStaffService _staffService;

        public OperationStaffController(IOperationStaffService staffService)
        {
            _staffService = staffService;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpGet("lane-assignment")]
        public async Task<IActionResult> GetTodayLaneAssignment()
        {
            var assignment = await _staffService.GetTodayLaneAssignmentAsync(GetUserId());
            if (assignment == null)
            {
                return Ok(new { Message = "No lane assigned for today." });
            }
            return Ok(assignment);
        }

        [HttpGet("tasks")]
        [HttpGet("/api/v1/staff/tasks/bookings")]
        public async Task<IActionResult> GetAssignedTasks([FromQuery] System.DateTime? date)
        {
            var tasks = await _staffService.GetAssignedBookingsAsync(GetUserId(), date);
            return Ok(tasks);
        }

        [HttpPost("lanes/swap")]
        public async Task<IActionResult> SwapShiftByPhone([FromBody] SwapLaneByPhoneDTO dto)
        {
            await _staffService.SwapShiftByPhoneAsync(GetUserId(), dto);
            return Ok(new { Message = "Shift swapped successfully." });
        }
        [HttpPost("bookings/{bookingId}/checkin")]
        public async Task<IActionResult> StaffCheckin(int bookingId)
        {
            await _staffService.CheckInBookingAsync(GetUserId(), bookingId);
            return Ok(new { Message = "Vehicle checked in and assigned to your lane successfully." });
        }
        [HttpPut("bookings/{bookingId}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int bookingId, [FromBody] UpdateBookingStatusDTO dto)
        {
            await _staffService.UpdateBookingStatusAsync(GetUserId(), bookingId, dto.Status);
            return Ok(new { Message = $"Booking status updated to {dto.Status}." });
        }
    }
}
