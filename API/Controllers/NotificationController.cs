using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly AutoWashDbContext _context;

        public NotificationController(AutoWashDbContext context)
        {
            _context = context;
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

            // Find token globally across all users
            var existingToken = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.Token == request.Token);

            if (existingToken == null)
            {
                var newToken = new UserFcmToken
                {
                    UserId = userId,
                    Token = request.Token,
                    CreatedAt = System.DateTime.UtcNow,
                    LastUsedAt = System.DateTime.UtcNow
                };
                _context.UserFcmTokens.Add(newToken);
            }
            else
            {
                // Reassign token to current user if it belonged to someone else
                if (existingToken.UserId != userId)
                {
                    existingToken.UserId = userId;
                }
                existingToken.LastUsedAt = System.DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

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

            var existingToken = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

            if (existingToken != null)
            {
                _context.UserFcmTokens.Remove(existingToken);
                await _context.SaveChangesAsync();
            }

            return Ok(new { statusCode = 200, message = "FCM token removed successfully.", data = (object?)null, details = (object?)null });
        }
    }
}
