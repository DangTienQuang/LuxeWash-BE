using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Manager
{
    [Route("api/v1/manager/work-shifts")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerWorkShiftController : ControllerBase
    {
        private readonly IStaffManagementService _staffService;

        public ManagerWorkShiftController(IStaffManagementService staffService)
        {
            _staffService = staffService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkShifts([FromQuery] bool includeInactive = false)
        {
            var result = await _staffService.GetWorkShiftsAsync(includeInactive);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkShift([FromBody] CreateWorkShiftDTO request)
        {
            var result = await _staffService.CreateWorkShiftAsync(request);
            return Created("", new { statusCode = 201, message = "Work shift created successfully.", data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkShift(int id, [FromBody] UpdateWorkShiftDTO request)
        {
            var result = await _staffService.UpdateWorkShiftAsync(id, request);
            return Ok(new { statusCode = 200, message = "Work shift updated successfully.", data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkShift(int id)
        {
            await _staffService.DeleteWorkShiftAsync(id);
            return Ok(new { statusCode = 200, message = "Work shift deleted or deactivated successfully." });
        }
    }
}
