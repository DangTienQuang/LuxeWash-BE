using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Manager
{
    [Route("api/v1/manager/shift-assignments")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerShiftAssignmentController : ControllerBase
    {
        private readonly IStaffManagementService _staffService;

        public ManagerShiftAssignmentController(IStaffManagementService staffService)
        {
            _staffService = staffService;
        }

        [HttpGet]
        public async Task<IActionResult> GetShiftAssignments([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] int? staffUserId = null)
        {
            var result = await _staffService.GetShiftAssignmentsAsync(fromDate, toDate, staffUserId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost]
        public async Task<IActionResult> CreateShiftAssignment([FromBody] CreateShiftAssignmentDTO request)
        {
            var result = await _staffService.CreateShiftAssignmentAsync(request);
            return Created("", new { statusCode = 201, message = "Shift assigned successfully.", data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateShiftAssignment(int id, [FromBody] UpdateShiftAssignmentDTO request)
        {
            var result = await _staffService.UpdateShiftAssignmentAsync(id, request);
            return Ok(new { statusCode = 200, message = "Shift assignment updated successfully.", data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShiftAssignment(int id)
        {
            await _staffService.DeleteShiftAssignmentAsync(id);
            return Ok(new { statusCode = 200, message = "Shift assignment deleted successfully." });
        }
    }
}
