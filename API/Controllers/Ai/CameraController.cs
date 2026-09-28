using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Services.Operations;
using AutoWashPro.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace API.Controllers.AI
{
    [ApiController]
    [Route("api/v1/camera")]
    [Authorize(Roles = "Staff,Manager")]
    public class CameraController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly ILaneDisplayPublisherService _publisherService;
        private readonly AutoWashDbContext _context;
        private readonly ILogger<CameraController> _logger;

        public CameraController(
            IBookingService bookingService,
            ILaneDisplayPublisherService publisherService,
            AutoWashDbContext context,
            ILogger<CameraController> logger)
        {
            _bookingService = bookingService;
            _publisherService = publisherService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckInByCamera(
            [FromQuery] string plate,
            [FromForm] CheckInRequestDTO request)
        {
            var normalizedPlate = NormalizePlate(plate);
            if (string.IsNullOrEmpty(normalizedPlate))
            {
                return BadRequest(new { statusCode = 400, message = "Plate is required." });
            }

            var isAlreadyCheckedIn = await _context.Bookings.AnyAsync(b =>
                (b.LicensePlate == normalizedPlate ||
                    (b.Vehicle != null && b.Vehicle.LicensePlate == normalizedPlate)) &&
                (b.Status == "CheckedIn" || b.Status == "Processing"));

            if (isAlreadyCheckedIn)
            {
                return Ok(new
                {
                    statusCode = 200,
                    message = "Duplicate check-in skipped (already processing).",
                    isDuplicate = true
                });
            }

            await PublishReadingEventAsync(normalizedPlate);

            var result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(
                normalizedPlate,
                "CheckedIn",
                request.CheckInImage,
                request.AllowOutsideScheduledTime);

            if (result.IsWaitingForLane)
            {
                return Ok(new
                {
                    statusCode = 200,
                    message = "Check-in successful. All bays are currently busy.",
                    isWaiting = true,
                    data = result
                });
            }

            try
            {
                result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(
                    normalizedPlate,
                    "Processing");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
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

        [HttpPost("check-out")]
        public async Task<IActionResult> AutoCheckOutByCamera(
            [FromQuery] string plate,
            [FromForm] CheckOutRequestDTO request)
        {
            var normalizedPlate = NormalizePlate(plate);
            if (string.IsNullOrEmpty(normalizedPlate))
            {
                return BadRequest(new { statusCode = 400, message = "Plate is required." });
            }

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

        private async Task PublishReadingEventAsync(string normalizedPlate)
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdValue, out var userId)) return;

            var branchId = await _context.EmployeeProfiles
                .Where(profile => profile.EmployeeId == userId)
                .Select(profile => profile.BranchId)
                .FirstOrDefaultAsync();
            if (!branchId.HasValue) return;

            try
            {
                await _publisherService.PublishEventAsync(new LaneDisplayEventDTO
                {
                    Type = "reading",
                    BranchId = branchId.Value,
                    LicensePlate = normalizedPlate,
                    DisplayUntil = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddSeconds(12)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to publish camera reading event for plate {Plate}.",
                    normalizedPlate);
            }
        }

        private static string NormalizePlate(string? plate)
        {
            return string.IsNullOrWhiteSpace(plate)
                ? string.Empty
                : plate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpperInvariant();
        }
    }
}
