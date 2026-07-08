using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.AI
{
    [Route("api/v1/automated-wash")]
    [ApiController]
    [AllowAnonymous]
    public class AutomatedWashController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public AutomatedWashController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckIn([FromQuery] string plate, [FromQuery] int branchId = 1, [FromQuery] bool autoStart = true)
        {
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
