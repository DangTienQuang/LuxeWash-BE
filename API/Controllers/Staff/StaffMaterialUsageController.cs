using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Staff
{
    [ApiController]
    [Route("api/v1/staff/material-usages")]
    [Authorize(Roles = "Staff")]
    public class StaffMaterialUsageController : ControllerBase
    {
        private readonly IBookingMaterialUsageService _bookingMaterialUsageService;

        public StaffMaterialUsageController(IBookingMaterialUsageService bookingMaterialUsageService)
        {
            _bookingMaterialUsageService = bookingMaterialUsageService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("bookings/{bookingId}/extra")]
        public async Task<IActionResult> ReportExtraUsage(int bookingId, [FromBody] ReportExtraMaterialUsageDTO dto)
        {
            return Ok(await _bookingMaterialUsageService.CreateExtraUsageRequestAsync(bookingId, GetUserId(), dto));
        }
    }
}
