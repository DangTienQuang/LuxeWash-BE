using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [Route("api/v1/admin/users")]
    [ApiController]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class AdminUserController : ControllerBase
    {
        private readonly IUserService _userService;

        public AdminUserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCustomers([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, [FromQuery] string? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _userService.GetAllCustomersAsync(page, pageSize, keyword, status);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomerDetail(int id)
        {
            var result = await _userService.GetCustomerDetailByAdminAsync(id);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateUserStatusDTO request)
        {
            await _userService.UpdateCustomerStatusAsync(id, request.Status);
            var statusMsg = request.Status == "Active" ? "Unlocked" : "Locked";
            return Ok(new { statusCode = 200, message = $"{statusMsg} account successfully." });
        }

        [HttpPost("sync-points")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SyncCustomerPoints()
        {
            await _userService.SyncCustomerProfilePointsAsync();
            return Ok(new { statusCode = 200, message = "Customer points synced successfully." });
        }
    }
}