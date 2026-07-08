using AutoWashPro.BLL.DTOs;

using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace API.Controllers.Staff
{
    [Route("api/v1/admin/bookings")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class StaffBookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public StaffBookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBookingsByDate([FromQuery] DateTime targetDate)
        {
            var result = await _bookingService.GetAllBookingsByDateAsync(targetDate);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromQuery] string newStatus)
        {
            await _bookingService.UpdateBookingStatusAsync(id, newStatus);
            return Ok(new { statusCode = 200, message = $"Booking status updated to: {newStatus}" });
        }

        [HttpPut("status-by-license-plate")]
        public async Task<IActionResult> UpdateBookingStatusByLicensePlate([FromBody] UpdateBookingStatusByPlateDTO request)
        {
            var result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(request.LicensePlate, request.NewStatus);
            return Ok(new { statusCode = 200, message = $"Vehicle {request.LicensePlate} status updated to: {request.NewStatus}", data = result });
        }
        [HttpGet("by-license-plate/{licensePlate}")]
        public async Task<IActionResult> GetBookingsByLicensePlate(string licensePlate)
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;
            if (string.IsNullOrEmpty(branchIdClaim) || !int.TryParse(branchIdClaim, out int branchId))
            {
                throw new AutoWashPro.BLL.Exceptions.UnauthorizedException("Branch information (BranchId) not found in token.");
            }

            var result = await _bookingService.LookupLicensePlateAsync(licensePlate, branchId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }
        [HttpPut("{id}/no-show")]
        public async Task<IActionResult> MarkAsNoShow(int id)
        {
            await _bookingService.MarkAsNoShowAsync(id);
            return Ok(new { statusCode = 200, message = "Customer marked as No-Show successfully." });
        }

        [HttpPut("{detailId}/report-mismatch")]
        public async Task<IActionResult> ReportMismatch(int detailId, [FromQuery] AutoWashPro.BLL.Enums.VehicleConditionEnum condition, [FromQuery] int actualTypeId)
        {
            await _bookingService.ReportMismatchAsync(detailId, condition, actualTypeId);
            return Ok(new { statusCode = 200, message = "Vehicle condition updated and surcharge recalculated successfully." });
        }
        [HttpPost("force-cancel")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> ForceCancelBookings([FromBody] ForceCancelRequestDTO request)
        {
            await _bookingService.ForceCancelBookingsAsync(request);
            return Ok(new { statusCode = 200, message = "Bookings cancelled successfully. Refunds processed and notification emails sent to customers." });
        }
    }
}
