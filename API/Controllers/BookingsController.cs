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
                throw new AutoWashPro.BLL.Exceptions.UnauthorizedException("Không tìm thấy thông tin xác thực (UserId). Vui lòng đăng nhập lại.");
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

        // ĐỔI SANG POST ĐỂ NHẬN JSON BODY TỪ FRONTEND
        [HttpPost("available-slots")]
        public async Task<IActionResult> GetAvailableSlots([FromBody] CheckAvailableSlotsRequestDTO request)
        {
            int userId = GetUserId();
            // Đảm bảo Service của bạn cũng đã đổi tham số nhận vào thành CheckAvailableSlotsRequestDTO nhé!
            var result = await _bookingService.GetAvailableSlotsAsync(userId, request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }
        [HttpPost("{bookingId}/trigger-email")]
        [Authorize]
        public IActionResult TriggerConfirmationEmail(int bookingId)
        {
            // Lấy UserId từ Token (Giả sử bạn dùng ClaimHelper)
            var userId = ClaimHelper.GetUserId(User);

            // Bắn một Background Task độc lập với luồng HTTP hiện tại
            _ = Task.Run(async () =>
            {
                // TẠO MỘT SCOPE MỚI: Rất quan trọng để DbContext không bị Dispose khi API trả về 202
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Xin hệ thống cấp cho 1 instance IBookingService MỚI, đi kèm với 1 DbContext MỚI
                    var scopedBookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                    
                    // Thực thi hàm gửi mail bằng instance mới này
                    await scopedBookingService.SendBookingConfirmationEmailAsync(userId, bookingId);
                }
            });

            // Lập tức trả về cho Frontend mà không cần chờ mail gửi xong
            // Dùng 202 Accepted: Báo cho FE biết "Hệ thống đã ghi nhận yêu cầu và đang xử lý ngầm"
            return Accepted(new { 
                statusCode = 202, 
                message = "Hệ thống đang tiến hành gửi email xác nhận." 
            });
        }
        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.CreateBookingAsync(userId, request);
            var message = string.Equals(request.PaymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.PaymentMethod, "QR", StringComparison.OrdinalIgnoreCase)
                ? "Đặt lịch thành công. Vui lòng tạo QR để thanh toán."
                : "Đặt lịch và thanh toán bằng ví thành công.";
            return Created("", new { statusCode = 201, message, data = result });
        }

        [HttpPost("{id}/payment-link")]
        public async Task<IActionResult> CreateBookingPaymentLink(int id, [FromBody] CreateBookingPaymentLinkDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.CreateBookingPaymentLinkAsync(userId, id, request);
            return Ok(new { statusCode = 200, message = "Tạo QR thanh toán booking thành công.", data = result });
        }

        [Authorize(Roles = "Staff,Manager,Admin")]
        [HttpPost("walk-in")]
        public async Task<IActionResult> CreateWalkInBooking([FromBody] CreateWalkInBookingDTO request)
        {
            int staffId = GetUserId();
            var result = await _bookingService.CreateWalkInBookingAsync(staffId, request);
            return Created("", new { statusCode = 201, message = "Tạo lịch vãng lai thành công.", data = result });
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyBookings()
        {
            int userId = GetUserId();
            var result = await _bookingService.GetMyBookingsAsync(userId);
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
            return Ok(new { statusCode = 200, message = "Đã hủy lịch thành công." });
        }

        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> RescheduleBooking(int id, [FromBody] RescheduleBookingDTO request)
        {
            int userId = GetUserId();
            var result = await _bookingService.RescheduleBookingAsync(userId, id, request);
            return Ok(new { statusCode = 200, message = "Đã thay đổi lịch hẹn thành công.", data = result });
        }

        [Authorize(Roles = "Staff,Manager,Admin")]
        [HttpPut("{id}/condition")]
        public async Task<IActionResult> UpdateVehicleCondition(int id, [FromBody] UpdateVehicleConditionDTO request)
        {
            int staffId = GetUserId();
            await _bookingService.UpdateVehicleConditionAsync(staffId, id, request);
            return Ok(new { statusCode = 200, message = "Đã cập nhật tình trạng xe và áp dụng phụ phí thành công." });
        }

        [HttpGet("{id}/payment-status")]
        public async Task<IActionResult> GetBookingPaymentStatus(int id)
        {
            var result = await _bookingService.GetBookingPaymentStatusAsync(id);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }
    }
}
