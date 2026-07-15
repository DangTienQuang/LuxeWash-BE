using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.AI
{
    [Route("api/v1/camera")]
    [ApiController]
    [AllowAnonymous]
    public class CameraController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public CameraController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckInByCamera([FromQuery] string plate)
        {
            try
            {
                var result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(plate, "CheckedIn");
                return Ok(new { statusCode = 200, message = "Vehicle is valid, opening barrier!", data = result });
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