using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BLL.Helpers;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/bookings")]
    [ApiController]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly IServiceProvider _serviceProvider;

        public BookingsController(IBookingService bookingService, IServiceProvider serviceProvider)
        {
            _bookingService = bookingService;
            _serviceProvider = serviceProvider;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new AutoWashPro.BLL.Exceptions.UnauthorizedException("Authentication information not found (UserId). Please log in again.");
            }
            return userId;
        }

        [HttpPost("check-compatibility")]
        public async Task<IActionResult> CheckCompatibility([FromBody] CheckCompatibilityRequestDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.CheckCompatibilityAsync(userId, request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("available-slots")]
        public async Task<IActionResult> GetAvailableSlots([FromBody] CheckAvailableSlotsRequestDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.GetAvailableSlotsAsync(userId, request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("check-slots-with-suggestions")]
        public async Task<IActionResult> CheckSlotsWithSuggestions([FromBody] CheckAvailableSlotsRequestDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.GetAvailableSlotsWithSuggestionAsync(userId, request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("{bookingId}/trigger-email")]
        [Authorize]
        public IActionResult TriggerConfirmationEmail(int bookingId)
        {
            var userId = ClaimHelper.GetUserId(User);

            _ = Task.Run(async () =>
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var scopedBookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                    
                    await scopedBookingService.SendBookingConfirmationEmailAsync(userId, bookingId);
                }
            });

            return Accepted(new { 
                statusCode = 202, 
                message = "The system is processing the confirmation email." 
            });
        }
        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.CreateBookingAsync(userId, request);
            var message = string.Equals(request.PaymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.PaymentMethod, "QR", StringComparison.OrdinalIgnoreCase)
                ? "Booking created successfully. Please generate a QR code to complete payment."
                : "Booking created and payment via wallet completed successfully.";
            return Created("", new { statusCode = 201, message, data = result });
        }

        [HttpPost("{id}/payment-link")]
        public async Task<IActionResult> CreateBookingPaymentLink(int id, [FromBody] CreateBookingPaymentLinkDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.CreateBookingPaymentLinkAsync(userId, id, request);
            return Ok(new { statusCode = 200, message = "Booking payment QR code created successfully.", data = result });
        }

        [Authorize(Roles = "Staff,Manager,Admin")]
        [HttpPost("walk-in")]
        public async Task<IActionResult> CreateWalkInBooking([FromBody] CreateWalkInBookingDTO request)
        {
            int staffId = GetUserId();
            var result = await _bookingService.CreateWalkInBookingAsync(staffId, request);
            return Created("", new { statusCode = 201, message = "Walk-in booking created successfully.", data = result });
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyBookings()
        {
            int userId = GetUserId();
            var result = await _bookingService.GetMyBookingsAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("relocation-proposals")]
        public async Task<IActionResult> GetRelocationProposals()
        {
            int userId = GetUserId();
            var result = await _bookingService.GetRelocationProposalsAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [Authorize(Roles = "Admin,Manager,Staff")]
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetBookingsByUserId(int userId)
        {
            var result = await _bookingService.GetMyBookingsAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingById(int id)
        {
            int userId = GetUserId();
            var result = await _bookingService.GetBookingByIdAsync(userId, id);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            int userId = GetUserId();
            await _bookingService.CancelBookingAsync(userId, id);
            return Ok(new { statusCode = 200, message = "Booking cancelled successfully." });
        }

        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> RescheduleBooking(int id, [FromBody] RescheduleBookingDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.RescheduleBookingAsync(userId, id, request);
            return Ok(new { statusCode = 200, message = "Booking rescheduled successfully.", data = result });
        }

        [Authorize(Roles = "Staff,Manager,Admin")]
        [HttpPut("{id}/condition")]
        public async Task<IActionResult> UpdateVehicleCondition(int id, [FromBody] UpdateVehicleConditionDTO request)
        {
            int staffId = GetUserId();
            await _bookingService.UpdateVehicleConditionAsync(staffId, id, request);
            return Ok(new { statusCode = 200, message = "Vehicle condition updated and surcharge applied successfully." });
        }

        [HttpGet("{id}/payment-status")]
        public async Task<IActionResult> GetBookingPaymentStatus(int id)
        {
            var result = await _bookingService.GetBookingPaymentStatusAsync(id);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        /// <remarks>
        /// DEPRECATED: This endpoint is no longer supported.
        /// Use POST /{id}/handle-overload-suggestion with body {"decision":"Switch"} instead.
        /// </remarks>
        [Authorize]
        [HttpPost("{id}/accept-relocation")]
        [Obsolete("Use handle-overload-suggestion instead.")]
        public IActionResult AcceptRelocation(int id)
        {
            return StatusCode(410, new
            {
                statusCode = 410,
                message = "This endpoint has been removed. Please use POST /api/v1/bookings/{id}/handle-overload-suggestion with body {\"decision\":\"Switch\"} instead.",
                data = (object?)null
            });
        }

        [Authorize]
        [HttpGet("{id}/overload-suggestion")]
        public async Task<IActionResult> GetPendingOverloadSuggestion(int id)
        {
            var result = await _bookingService.GetPendingOverloadSuggestionAsync(GetUserId(), id);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [Authorize]
        [HttpPost("{id}/handle-overload-suggestion")]
        public async Task<IActionResult> HandleOverloadSuggestion(int id, [FromBody] HandleOverloadDecisionDTO request)
        {
            var result = await _bookingService.HandleOverloadDecisionAsync(GetUserId(), id, request);
            return Ok(new { statusCode = 200, message = "Overload suggestion handled successfully.", data = result });
        }
    }
}
