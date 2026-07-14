using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.User
{
    [Route("api/v1/users")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized(new { statusCode = 401, message = "Unauthorized" });

            int userId = int.Parse(userIdClaim);
            var result = await _userService.GetProfileAsync(userId);

            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateUserProfileDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized(new { statusCode = 401, message = "Unauthorized" });

            int userId = int.Parse(userIdClaim);
            await _userService.UpdateProfileAsync(userId, request);

            return Ok(new { statusCode = 200, message = "Personal information updated successfully." });
        }

        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMyAccount()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized(new { statusCode = 401, message = "Unauthorized" });

            int userId = int.Parse(userIdClaim);
            await _userService.DeleteAccountAsync(userId);

            return Ok(new { statusCode = 200, message = "Account deleted successfully." });
        }
    }
}