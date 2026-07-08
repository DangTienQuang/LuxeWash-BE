using AutoWashPro.BLL.Exceptions;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Helpers;
using BLL.Services;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/v1/business")]
    [Authorize]
    public class BusinessController : ControllerBase
    {
        private readonly IBusinessService _businessService;
        private readonly IInvoiceService _invoiceService;
        private readonly IBookingAttendanceService _attendanceService;
        private readonly IFleetService _fleetService;
        private readonly IBusinessBookingService _businessBookingService;
        private readonly IInvoicePdfService _invoicePdfService;

        public BusinessController(IBusinessService businessService, IInvoiceService invoiceService,
            IBookingAttendanceService attendanceService, IFleetService fleetService, IBusinessBookingService businessBookingService,
                IInvoicePdfService invoicePdfService)
        {
            _businessService = businessService;
            _invoiceService = invoiceService;
            _attendanceService = attendanceService;
            _fleetService = fleetService;
            _businessBookingService = businessBookingService;
            _invoicePdfService = invoicePdfService;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> RegisterBusinessUser([FromForm] RegisterBusinessUserRequest request)
        {
            var result = await _businessService.RegisterBusinessUserAsync(request);
            return Ok(new
            {
                statusCode = 200,
                message = "Business account registered successfully. Awaiting administrator approval.",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("my-profile")]
        public async Task<IActionResult> GetMyBusinessProfile()
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessService.GetByUserIdAsync(userId);

            if (result == null)
                throw new NotFoundException("Business profile not found.");

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/review-application")]
        public async Task<IActionResult> ReviewBusinessProfile([FromBody] ReviewBusinessProfileDTO dto)
        {
            if (dto == null)
                throw new BadRequestException("Review data is required.");

            int reviewerId = ClaimHelper.GetUserId(User);

            await _businessService.ReviewBusinessProfileAsync(reviewerId, dto);

            return Ok(new
            {
                statusCode = 200,
                message = "Business profile reviewed successfully."
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/pending-applications")]
        public async Task<IActionResult> GetPendingApplications()
        {
            var result = await _businessService.GetPendingBusinessApplicationsAsync();

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/application/{businessProfileId}")]
        public async Task<IActionResult> GetApplicationDetail(int businessProfileId)
        {
            if (businessProfileId <= 0)
                throw new BadRequestException("Profile ID is invalid.");

            var result = await _businessService.GetBusinessApplicationDetailAsync(businessProfileId);

            if (result == null)
                throw new NotFoundException($"Profile with ID {businessProfileId} not found.");

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("available-slots")]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] CheckBusinessSlotsRequestDTO request)
        {
            int userId = ClaimHelper.GetUserId(User);
            var result = await _businessBookingService.GetAvailableSlotsForBusinessAsync(userId, request);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpPost("bookings")]
        public async Task<IActionResult> CreateBooking(CreateBusinessBookingDTO dto)
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.CreateBusinessBookingAsync(userId, dto);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpPut("reschedule/{id}")]
        public async Task<IActionResult> RescheduleBooking(int id, [FromBody] RescheduleBusinessBookingDTO dto)
        {
            int userId = ClaimHelper.GetUserId(User);
            dto.BookingId = id;

            var result = await _businessBookingService.RescheduleBookingAsync(userId, dto);

            return Ok(new
            {
                statusCode = 200,
                message = "Booking rescheduled successfully.",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("vehicles")]
        public async Task<IActionResult> GetActiveVehicles()
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetActiveFleetVehiclesAsync(userId);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("vehicles/status/all")]
        public async Task<IActionResult> GetActiveVehiclesOnFloor()
        {
            int userId = ClaimHelper.GetUserId(User);
            var result = await _businessBookingService.GetActiveVehiclesOnFloorAsync(userId);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("vehicles/status")]
        public async Task<IActionResult> GetVehiclesByStatus([FromQuery] string? status = null)
        {
            int userId = ClaimHelper.GetUserId(User);
            var result = await _businessBookingService.GetVehiclesByStatusAsync(userId, status);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet]
        public async Task<IActionResult> GetBookings()
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetBookingsAsync(userId);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingDetail(int id)
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetBookingDetailAsync(userId, id);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            int userId = ClaimHelper.GetUserId(User);

            await _businessBookingService.CancelBookingAsync(userId, id);

            return Ok(new
            {
                statusCode = 200,
                message = "Booking cancelled successfully."
            });
        }

        [HttpGet("invoice/{bookingId}")]
        public async Task<IActionResult> GetInvoice(int bookingId)
        {
            var result = await _businessBookingService.GetInvoiceByBookingAsync(bookingId);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] FleetHistoryFilterDTO filter)
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetFleetWashHistoryAsync(userId, filter);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetDashboardAsync(userId);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Business")]
        [HttpGet("statements/monthly")]
        public async Task<IActionResult> GetMonthlyStatement([FromQuery] int year, [FromQuery] int month)
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService
                    .GetMonthlyStatementAsync(
                        userId,
                        year,
                        month);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("washlogs/{washLogId}/assign-lane")]
        public async Task<IActionResult> AssignLane(int washLogId, AssignLaneDTO dto)
        {
            await _businessBookingService.AssignLaneAsync(washLogId, dto);

            return Ok(new
            {
                statusCode = 200,
                message = "Lane assigned to vehicle successfully."
            });
        }

        [HttpGet("invoices/{invoiceId}/export")]
        [Authorize(Roles = "Business,Manager,Staff")]
        public async Task<IActionResult> ExportInvoice(int invoiceId)
        {
            var result =
                await _businessService.GetInvoiceExportAsync(invoiceId);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = result
            });
        }
    }
}