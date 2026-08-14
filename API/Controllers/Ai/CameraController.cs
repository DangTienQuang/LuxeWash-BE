using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace API.Controllers.AI
{
    [Route("api/v1/camera")]
    [ApiController]
    [Authorize(Roles = "Staff,Manager")]
    public class CameraController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly AutoWashPro.BLL.Services.Operations.ILaneDisplayPublisherService _publisherService;
        private readonly AutoWashPro.DAL.Data.AutoWashDbContext _context;
        private readonly ILogger<CameraController> _logger;

        public CameraController(
            IBookingService bookingService,
            AutoWashPro.BLL.Services.Operations.ILaneDisplayPublisherService publisherService,
            AutoWashPro.DAL.Data.AutoWashDbContext context,
            ILogger<CameraController> logger)
        {
            _bookingService = bookingService;
            _publisherService = publisherService;
            _context = context;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckInByCamera(
            [FromQuery] string plate,
            [FromForm] CheckInRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(plate)) return BadRequest("Plate is required");
            var normalizedPlate = plate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper();

            // DB-based Deduplication logic (Replaces MemoryCache to support multi-server)
            var isAlreadyCheckedIn = await _context.Bookings.AnyAsync(b => 
                (b.LicensePlate == normalizedPlate || (b.Vehicle != null && b.Vehicle.LicensePlate == normalizedPlate))
                && b.ScheduledTime >= DateTime.UtcNow.AddHours(-12)
                && (b.Status == "CheckedIn" || b.Status == "Processing"));

            if (isAlreadyCheckedIn)
            {
                return Ok(new { statusCode = 200, message = "Duplicate check-in skipped (already processing).", isDuplicate = true });
            }

            try
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var employeeProfile = await _context.EmployeeProfiles.FindAsync(userId);
                    if (employeeProfile != null && employeeProfile.BranchId.HasValue)
                    {
                        // Wrap reading event separately - failure must NOT block booking update (Section 6.5)
                        try
                        {
                            await _publisherService.PublishEventAsync(new AutoWashPro.BLL.DTOs.Operations.LaneDisplayEventDTO
                            {
                                Type = "reading",
                                BranchId = employeeProfile.BranchId.Value,
                                LicensePlate = normalizedPlate,
                                DisplayUntil = System.DateTime.UtcNow.AddSeconds(12)
                            });
                        }
                        catch (Exception readingEx)
                        {
                            // 'reading' event is display-only. Never block check-in for this.
                            _logger.LogWarning(readingEx,
                                "Unable to publish camera reading event for plate {Plate}. Check-in will continue.",
                                normalizedPlate);
                        }
                    }
                }

                var result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(
                    normalizedPlate,
                    "CheckedIn",
                    request.CheckInImage,
                    request.AllowOutsideScheduledTime);
                if (result.IsWaitingForLane)
                {
                    return Ok(new { statusCode = 200, message = "Check-in successful! All bays are currently busy. Please wait before the barrier.", isWaiting = true, data = result });
                }

                try
                {
                    result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(
                        normalizedPlate,
                        "Processing");
                }
                catch (Exception startEx)
                {
                    // Check-in has already succeeded. Keep that result so the frontend can
                    // retry auto-start without accidentally creating another check-in.
                    _logger.LogWarning(
                        startEx,
                        "Vehicle {Plate} checked in but could not start washing automatically.",
                        normalizedPlate);
                    return Ok(new
                    {
                        statusCode = 200,
                        message = "Check-in successful, but the wash could not start automatically.",
                        isWaiting = false,
                        autoStartFailed = true,
                        data = result
                    });
                }

                return Ok(new
                {
                    statusCode = 200,
                    message = "Check-in successful and wash started automatically.",
                    isWaiting = false,
                    autoStarted = true,
                    data = result
                });
            }
            catch (AutoWashPro.BLL.Exceptions.BadRequestException ex)
            {
                return BadRequest(new
                {
                    statusCode = 400,
                    errorCode = ex.ErrorCode,
                    message = ex.Message
                });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("check-out")]
        public async Task<IActionResult> AutoCheckOutByCamera(
            [FromQuery] string plate,
            [FromForm] CheckOutRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(plate)) return BadRequest("Plate is required");
            var normalizedPlate = plate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper();

            try
            {
                var result = await _bookingService.AutoCheckOutByLicensePlateAsync(
                    normalizedPlate,
                    request.CheckOutImage);
                return Ok(new
                {
                    statusCode = 200,
                    message = result.IsDuplicate
                        ? "Duplicate check-out skipped (recently completed)."
                        : "Vehicle check-out completed, barrier opening!",
                    isDuplicate = result.IsDuplicate,
                    data = result
                });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }
    }
}
