using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.API.Controllers
{
    [Route("api/[controller]")]
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

        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] FcmTokenDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest("Token is required.");

            // Check if token already exists for this user
            var existingToken = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

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
                await _context.SaveChangesAsync();
            }
            else
            {
                existingToken.LastUsedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "FCM token registered successfully." });
        }

        [HttpDelete("remove-token")]
        public async Task<IActionResult> RemoveToken([FromBody] FcmTokenDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest("Token is required.");

            var existingToken = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

            if (existingToken != null)
            {
                _context.UserFcmTokens.Remove(existingToken);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "FCM token removed successfully." });
        }
    }
}
