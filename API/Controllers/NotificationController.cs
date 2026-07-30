using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoWashPro.BLL.Services.Interface;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly IPushNotificationService _pushNotificationService;

        public NotificationController(IPushNotificationService pushNotificationService)
        {
            _pushNotificationService = pushNotificationService;
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
    }
}
