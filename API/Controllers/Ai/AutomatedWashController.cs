using API.Controllers.Operations;
using AutoWashPro.BLL.Services;
using BLL.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace API.Controllers.AI
{
    [Route("api/v1/automated-wash")]
    [ApiController]
    [AllowAnonymous]
    public class AutomatedWashController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly BarrierDeviceOptions _deviceOptions;

        public AutomatedWashController(IBookingService bookingService, IOptions<BarrierDeviceOptions> deviceOptions)
        {
            _bookingService = bookingService;
            _deviceOptions = deviceOptions.Value;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckIn([FromQuery] string plate, [FromQuery] int branchId = 1, [FromQuery] bool autoStart = true)
        {
            if (string.IsNullOrWhiteSpace(_deviceOptions.DeviceId) || string.IsNullOrWhiteSpace(_deviceOptions.DeviceKey))
                return StatusCode(503, new { statusCode = 503, message = "Automated check-in device is not configured." });

            var deviceId = Request.Headers["X-Device-Id"].ToString();
            var deviceKey = Request.Headers["X-Device-Key"].ToString();
            if (!DeviceAuthHelper.IsValidDevice(deviceId, deviceKey, _deviceOptions.DeviceId, _deviceOptions.DeviceKey))
                return Unauthorized(new { statusCode = 401, message = "Invalid device credentials." });

            try
            {
                var result = await _bookingService.AutoCheckInAndStartProcessingAsync(plate, branchId, autoStart);
                return Ok(new
                {
                    statusCode = 200,
                    message = autoStart 
                        ? $"Vehicle {plate} is valid! Barrier opened and wash cycle started automatically." 
                        : $"Vehicle {plate} is valid! Barrier opened for check-in.",
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
