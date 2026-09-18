using System.Security.Claims;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoWashPro.API.Controllers.Manager
{
    [Route("api/v1/manager/incidents")]
    [Route("api/manager/incidents")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    public class IncidentController : ControllerBase
    {
        private readonly IIncidentService _incidentService;

        public IncidentController(IIncidentService incidentService)
        {
            _incidentService = incidentService;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue("userId") ?? "0");
        }

        [HttpPost("preview")]
        public async Task<IActionResult> PreviewIncident([FromBody] PreviewIncidentRequestDTO request)
        {
            var result = await _incidentService.PreviewIncidentImpactAsync(GetUserId(), request);
            return Ok(new { success = true, data = result });
        }

        [HttpPost]
        public async Task<IActionResult> CreateIncident([FromBody] CreateIncidentRequestDTO request)
        {
            var incidentId = await _incidentService.CreateIncidentAsync(GetUserId(), request);
            return Ok(new { success = true, message = "Incident created successfully", incidentId = incidentId });
        }

        [HttpPut("{id}/extend")]
        public async Task<IActionResult> ExtendIncident(long id, [FromBody] ExtendIncidentRequestDTO request)
        {
            await _incidentService.ExtendIncidentAsync(GetUserId(), id, request);
            return Ok(new { success = true, message = "Incident extended successfully" });
        }

        [HttpPost("{id}/resolve")]
        public async Task<IActionResult> ResolveIncident(long id)
        {
            await _incidentService.ResolveIncidentAsync(GetUserId(), id);
            return Ok(new { success = true, message = "Incident resolved successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> GetIncidents([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _incidentService.GetIncidentsAsync(GetUserId(), page, pageSize);
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetIncident(long id)
        {
            var result = await _incidentService.GetIncidentAsync(GetUserId(), id);
            return Ok(new { success = true, data = result });
        }

        [HttpGet("{id}/impact")]
        public async Task<IActionResult> GetIncidentImpact(long id)
        {
            var result = await _incidentService.GetIncidentImpactAsync(GetUserId(), id);
            return Ok(new { success = true, data = result });
        }
    }
}
