using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/admin/time-slots")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class TimeSlotsController : ControllerBase
    {
        private readonly ITimeSlotService _timeSlotService;

        public TimeSlotsController(ITimeSlotService timeSlotService)
        {
            _timeSlotService = timeSlotService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTimeSlots([FromQuery] int? branchId)
        {
            var result = await _timeSlotService.GetAllTimeSlotsAsync(branchId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTimeSlot([FromBody] CreateTimeSlotDTO request)
        {
            var result = await _timeSlotService.CreateTimeSlotAsync(request);
            return Created("", new { statusCode = 201, message = "Time slot created successfully.", data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTimeSlot(int id, [FromBody] UpdateTimeSlotDTO request)
        {
            var result = await _timeSlotService.UpdateTimeSlotAsync(id, request);
            return Ok(new { statusCode = 200, message = "Time slot updated successfully.", data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTimeSlot(int id)
        {
            await _timeSlotService.DeleteTimeSlotAsync(id);
            return Ok(new { statusCode = 200, message = "Time slot deleted successfully." });
        }
    }
}
