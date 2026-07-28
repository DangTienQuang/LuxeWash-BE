using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

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

        public CameraController(IBookingService bookingService, AutoWashPro.BLL.Services.Operations.ILaneDisplayPublisherService publisherService, AutoWashPro.DAL.Data.AutoWashDbContext context)
        {
            _bookingService = bookingService;
            _publisherService = publisherService;
            _context = context;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckInByCamera([FromQuery] string plate)
        {
            try
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var employeeProfile = await _context.EmployeeProfiles.FindAsync(userId);
                    if (employeeProfile != null && employeeProfile.BranchId.HasValue)
                    {
                        await _publisherService.PublishEventAsync(new AutoWashPro.BLL.DTOs.Operations.LaneDisplayEventDTO
                        {
                            Type = "reading",
                            BranchId = employeeProfile.BranchId.Value,
                            LicensePlate = plate,
                            DisplayUntil = System.DateTime.UtcNow.AddSeconds(12)
                        });
                    }
                }

                var result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(plate, "CheckedIn");
                if (result.IsWaitingForLane)
                {
                    return Ok(new { statusCode = 200, message = "Check-in successful! All bays are currently busy. Please wait before the barrier.", isWaiting = true, data = result });
                }
                return Ok(new { statusCode = 200, message = "Vehicle is valid, opening barrier!", isWaiting = false, data = result });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("check-out")]
        public async Task<IActionResult> AutoCheckOutByCamera([FromQuery] string plate)
        {
            try
            {
                var result = await _bookingService.AutoCheckOutByLicensePlateAsync(plate);
                return Ok(new { statusCode = 200, message = "Vehicle check-out completed, barrier opening!", data = result });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }
    }
}