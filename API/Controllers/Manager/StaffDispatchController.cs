using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Manager
{
    [ApiController]
    [Route("api/v1/manager/dispatches")]
    [Authorize(Roles = "Manager")]
    public class StaffDispatchController : ControllerBase
    {
        private readonly IStaffLaneDispatchService _dispatchService;

        public StaffDispatchController(IStaffLaneDispatchService dispatchService)
        {
            _dispatchService = dispatchService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateDispatch([FromBody] CreateStaffLaneDispatchDTO request)
        {
            var result = await _dispatchService.CreateDispatchAsync(GetUserId(), request);
            return Created(string.Empty, new
            {
                statusCode = 201,
                message = "Dispatch created and notification sent.",
                data = result
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDispatches(
            [FromQuery] string? status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var result = await _dispatchService.GetManagerDispatchesAsync(GetUserId(), status, from, to);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelDispatch(int id)
        {
            await _dispatchService.CancelDispatchAsync(GetUserId(), id);
            return Ok(new { statusCode = 200, message = "Dispatch cancelled successfully." });
        }

        [HttpGet("staff/{staffId}/active")]
        public async Task<IActionResult> GetActiveDispatchesForStaff(int staffId)
        {
            var result = await _dispatchService.GetActiveDispatchesForStaffAsync(GetUserId(), staffId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
