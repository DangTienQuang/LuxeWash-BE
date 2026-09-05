using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.Staff
{
    [ApiController]
    [Route("api/v1/operation-staff")]
    [Authorize(Roles = "Staff")]
    public class OperationStaffController : ControllerBase
    {
        private readonly IOperationStaffService _staffService;
        private readonly ILaneIncidentService _laneIncidentService;
        private readonly IStaffLaneDispatchService _dispatchService;

        public OperationStaffController(
            IOperationStaffService staffService,
            ILaneIncidentService laneIncidentService,
            IStaffLaneDispatchService dispatchService)
        {
            _staffService = staffService;
            _laneIncidentService = laneIncidentService;
            _dispatchService = dispatchService;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpGet("tasks")]
        [HttpGet("/api/v1/staff/tasks/bookings")]
        public async Task<IActionResult> GetAssignedTasks([FromQuery] System.DateTime? date)
        {
            var tasks = await _staffService.GetAssignedBookingsAsync(GetUserId(), date);
            return Ok(tasks);
        }


        [HttpPost("bookings/{bookingId}/checkin")]
        public async Task<IActionResult> StaffCheckin(int bookingId, [FromForm] CheckInRequestDTO dto)
        {
            var result = await _staffService.CheckInBookingAsync(
                GetUserId(),
                bookingId,
                dto.CheckInImage,
                dto.AllowOutsideScheduledTime);

            var message = result.IsWaiting
                ? "Check-in successful. All bays are currently busy — please wait before the barrier."
                : $"Vehicle admitted and assigned to {result.LaneName ?? "lane"}.";

            return Ok(new
            {
                statusCode = 200,
                message,
                data = new
                {
                    bookingId = result.BookingId,
                    licensePlate = result.LicensePlate,
                    status = result.Status,
                    admissionStatus = result.AdmissionStatus,
                    isWaiting = result.IsWaiting,
                    laneId = result.LaneId,
                    laneName = result.LaneName,
                    barrierCommandId = result.BarrierCommandId,
                    barrierCommandCreated = result.BarrierCommandCreated,
                    barrierId = result.BarrierId,
                    barrierCommandExpiresAt = result.BarrierCommandExpiresAt
                }
            });
        }
        [HttpPut("bookings/{bookingId}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int bookingId, [FromForm] UpdateBookingStatusDTO dto)
        {
            await _staffService.UpdateBookingStatusAsync(GetUserId(), bookingId, dto.Status, dto.CheckOutImage);
            return Ok(new { Message = $"Booking status updated to {dto.Status}." });
        }

        [HttpGet("lane-occupancies")]
        public async Task<IActionResult> GetLaneOccupancies()
        {
            var occupancies = await _staffService.GetActiveLaneOccupanciesAsync(GetUserId());
            return Ok(occupancies);
        }

        [HttpGet("lanes")]
        public async Task<IActionResult> GetLanes()
        {
            var lanes = await _laneIncidentService.GetStaffLanesAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Success", data = lanes });
        }

        [HttpGet("dispatches")]
        public async Task<IActionResult> GetDispatches([FromQuery] string? status)
        {
            var dispatches = await _dispatchService.GetStaffDispatchesAsync(GetUserId(), status);
            return Ok(new { statusCode = 200, message = "Success", data = dispatches });
        }

        [HttpPut("dispatches/{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeDispatch(int id)
        {
            var dispatch = await _dispatchService.AcknowledgeDispatchAsync(GetUserId(), id);
            return Ok(new { statusCode = 200, message = "Dispatch acknowledged.", data = dispatch });
        }

        [HttpPut("dispatches/{id}/complete")]
        public async Task<IActionResult> CompleteDispatch(int id, [FromBody] CompleteStaffLaneDispatchDTO dto)
        {
            var dispatch = await _dispatchService.CompleteDispatchAsync(GetUserId(), id, dto);
            return Ok(new { statusCode = 200, message = "Dispatch completed.", data = dispatch });
        }

        [HttpPost("lane-incidents")]
        public async Task<IActionResult> CreateLaneIncident([FromBody] CreateLaneIncidentDTO dto)
        {
            var incident = await _laneIncidentService.CreateStaffIncidentAsync(GetUserId(), dto);
            return Created(string.Empty, new
            {
                statusCode = 201,
                message = "Lane incident reported and manager notified.",
                data = incident
            });
        }

        [HttpGet("lane-incidents/my-reports")]
        public async Task<IActionResult> GetMyLaneIncidentReports()
        {
            var reports = await _laneIncidentService.GetStaffReportsAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Success", data = reports });
        }
    }
}
