using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AutoWashPro.BLL.Services.Operations;

namespace API.Controllers.Operations
{
    [Route("api/v1/barrier")]
    [ApiController]
    public class BarrierCommandController : ControllerBase
    {
        private readonly IOperationsMonitoringService _monitoringService;

        public BarrierCommandController(IOperationsMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }

        [HttpPost("ack")]
        public async Task<IActionResult> AckBarrierCommand([FromBody] BarrierAckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CommandId))
                return BadRequest(new { message = "CommandId is required." });

            var found = await _monitoringService.AckBarrierCommandAsync(request.CommandId, request.Status);

            if (!found)
                return NotFound(new { message = "Command not found." });

            return Ok(new { message = "ACK processed successfully." });
        }
    }

    public class BarrierAckRequest
    {
        public string CommandId { get; set; } = null!;
        public string? Status { get; set; }
        public string? Details { get; set; }
    }
}
