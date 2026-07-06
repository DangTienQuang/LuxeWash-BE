using AutoWashPro.BLL.Services;
using BLL.Services.Interface;
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
        private readonly IDataSeedingService _dataSeedingService;

        public CameraController(IBookingService bookingService, IDataSeedingService dataSeedingService)
        {
            _bookingService = bookingService;
            _dataSeedingService = dataSeedingService;
        }

        [HttpPost("check-in")]
        public async Task<IActionResult> AutoCheckInByCamera([FromQuery] string plate)
        {
            try
            {
                var result = await _bookingService.UpdateBookingStatusByLicensePlateAsync(plate, "CheckedIn");
                return Ok(new { statusCode = 200, message = "Xe hợp lệ, mở Barie!", data = result });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("seed-test-booking")]
        public async Task<IActionResult> SeedTestBooking([FromQuery] string plate = "30A-888.88")
        {
            try
            {
                var booking = await _dataSeedingService.SeedTestBookingForAIAsync(plate);
                return Ok(new
                {
                    statusCode = 200,
                    message = $"Đã khởi tạo thành công dữ liệu test cho biển số {plate} ở trạng thái Pending!",
                    data = new
                    {
                        bookingId = booking.BookingId,
                        licensePlate = booking.LicensePlate,
                        status = booking.Status,
                        scheduledTime = booking.ScheduledTime,
                        branchId = booking.BranchId,
                        finalAmount = booking.FinalAmount
                    }
                });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }
    }
}