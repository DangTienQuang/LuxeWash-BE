using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using AutoWashPro.DAL.Data;

namespace API.Controllers.Operations
{
    [Route("api/v1/barrier")]
    [ApiController]
    public class BarrierCommandController : ControllerBase
    {
        private readonly AutoWashPro.DAL.Data.AutoWashDbContext _context;

        public BarrierCommandController(AutoWashPro.DAL.Data.AutoWashDbContext context)
        {
            _context = context;
        }

        [HttpPost("ack")]
        public async Task<IActionResult> AckBarrierCommand([FromBody] BarrierAckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CommandId))
            {
                return BadRequest(new { message = "CommandId is required." });
            }

            var command = await _context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == request.CommandId);
            
            if (command == null)
            {
                return NotFound(new { message = "Command not found." });
            }

            command.Status = string.IsNullOrWhiteSpace(request.Status) ? "Completed" : request.Status;
            
            await _context.SaveChangesAsync();

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
