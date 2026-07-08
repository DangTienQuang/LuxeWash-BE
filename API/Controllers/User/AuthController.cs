using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.User
{
    [Route("api/v1/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO request)
        {
            var result = await _authService.RegisterAsync(request);
            return Created("", new { statusCode = 201, message = "Success", data = result });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDTO request)
        {
            var result = await _authService.VerifyOtpAsync(request);
            return Ok(new { statusCode = 200, message = "Email verified successfully.", data = result });
        }
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDTO request)
        {
            var response = await _authService.ResendOtpAsync(request);
            return Ok(new { statusCode = 200, message = "OTP resent successfully", data = response });
        }
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDTO request)
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [Authorize] 
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized(new { statusCode = 401, message = "Unauthorized" });

            int userId = int.Parse(userIdClaim);
            await _authService.ChangePasswordAsync(userId, request);

            return Ok(new { statusCode = 200, message = "Password changed successfully." });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null) return Unauthorized(new { statusCode = 401, message = "Unauthorized" });

            int userId = int.Parse(userIdClaim);
            await _authService.LogoutAsync(userId);

            return Ok(new { statusCode = 200, message = "Logged out successfully." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO request)
        {
            await _authService.ForgotPasswordAsync(request);
            return Ok(new { statusCode = 200, message = "Password reset OTP has been sent to your email." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO request)
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new { statusCode = 200, message = "Password reset successfully. Please log in again." });
        }
    }
}
