using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoWashPro.BLL.Services.Interface;
using System.Collections.Generic;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IUserNotificationService _userNotificationService;

        public NotificationController(IPushNotificationService pushNotificationService, IUserNotificationService userNotificationService)
        {
            _pushNotificationService = pushNotificationService;
            _userNotificationService = userNotificationService;
        }

        public class FcmTokenDto
        {
            public string Token { get; set; } = null!;
        }

        [HttpPost("token")]
        public async Task<IActionResult> RegisterToken([FromBody] FcmTokenDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { statusCode = 400, message = "Token is required.", data = (object?)null, details = (object?)null });

            await _pushNotificationService.RegisterTokenAsync(userId, request.Token);

            return Ok(new { statusCode = 200, message = "FCM token registered successfully.", data = (object?)null, details = (object?)null });
        }

        [HttpDelete("token")]
        public async Task<IActionResult> RemoveToken([FromBody] FcmTokenDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { statusCode = 400, message = "Token is required.", data = (object?)null, details = (object?)null });

            await _pushNotificationService.RemoveTokenAsync(userId, request.Token);

            return Ok(new { statusCode = 200, message = "FCM token removed successfully.", data = (object?)null, details = (object?)null });
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var notifications = await _userNotificationService.GetMyNotificationsAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = notifications, details = (object?)null });
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            var count = await _userNotificationService.GetUnreadCountAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = count, details = (object?)null });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            await _userNotificationService.MarkAsReadAsync(id, userId);
            return Ok(new { statusCode = 200, message = "Success", data = (object?)null, details = (object?)null });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            await _userNotificationService.MarkAllAsReadAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = (object?)null, details = (object?)null });
        }
    }
}
