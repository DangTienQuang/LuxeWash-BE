using System.Security.Claims;
using System.Threading.Tasks;
using AutoWashPro.BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoWashPro.API.Controllers.Customer
{
    [Route("api/v1/incidents/actions")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class IncidentActionController : ControllerBase
    {
        private readonly IIncidentCustomerService _incidentCustomerService;

        public IncidentActionController(IIncidentCustomerService incidentCustomerService)
        {
            _incidentCustomerService = incidentCustomerService;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue("userId") ?? "0");
        }

        [HttpPost("{affectedBookingId}/decision")]
        public async Task<IActionResult> MakeDecision(long affectedBookingId, [FromBody] CustomerDecisionRequest request)
        {
            await _incidentCustomerService.HandleCustomerDecisionAsync(GetUserId(), affectedBookingId, request.Decision, request.TargetBranchId, request.TargetSlotId);
            return Ok(new { success = true, message = "Decision processed successfully. A 20% discount voucher has been added to your account." });
        }

        [HttpGet("affected-booking-by-booking/{bookingId}")]
        public async Task<IActionResult> GetAffectedBookingDetails(int bookingId)
        {
            var result = await _incidentCustomerService.GetAffectedBookingDetailsAsync(GetUserId(), bookingId);
            return Ok(result);
        }
    }

    public class CustomerDecisionRequest
    {
        public string Decision { get; set; } = null!; // "Cancel", "Transfer"
        public int? TargetBranchId { get; set; }
        public int? TargetSlotId { get; set; }
    }
}
