using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Manager
{
    [ApiController]
    [Route("api/v1/manager")]
    [Authorize(Roles = "Manager")]
    public class LaneIncidentController : ControllerBase
    {
        private readonly ILaneIncidentService _laneIncidentService;

        public LaneIncidentController(ILaneIncidentService laneIncidentService)
        {
            _laneIncidentService = laneIncidentService;
        }

        [HttpGet("lanes/status")]
        public async Task<IActionResult> GetLaneStatuses()
        {
            var result = await _laneIncidentService.GetManagerLaneStatusesAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("lanes/{laneId}/reactivate")]
        public async Task<IActionResult> ReactivateLane(int laneId, [FromBody] ReactivateLaneDTO request)
        {
            var result = await _laneIncidentService.ReactivateLaneAsync(GetUserId(), laneId, request);
            return Ok(new { statusCode = 200, message = "Lane reactivated successfully.", data = result });
        }

        [HttpGet("incidents")]
        public async Task<IActionResult> GetIncidents([FromQuery] string? status)
        {
            var result = await _laneIncidentService.GetManagerIncidentsAsync(GetUserId(), status);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("incidents/{id}")]
        public async Task<IActionResult> GetIncident(int id)
        {
            var result = await _laneIncidentService.GetManagerIncidentAsync(GetUserId(), id);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("incidents/{id}/resolve")]
        public async Task<IActionResult> ResolveIncident(int id, [FromBody] ResolveLaneIncidentDTO request)
        {
            var result = await _laneIncidentService.ResolveIncidentAsync(GetUserId(), id, request);
            var message = request.Reactivated
                ? "Incident resolved and lane reactivated."
                : "Incident resolved. Lane remains inactive.";

            return Ok(new { statusCode = 200, message, data = result });
        }

        [HttpDelete("incidents/{id}")]
        public async Task<IActionResult> DeleteIncident(int id)
        {
            await _laneIncidentService.DeleteIncidentAsync(GetUserId(), id);
            return Ok(new { statusCode = 200, message = "Incident deleted successfully." });
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }
}
