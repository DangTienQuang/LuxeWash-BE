using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using BLL.DTOs.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API.Controllers.Manager
{
    [ApiController]
    [Route("api/v1/manager")]
    [Authorize(Roles = "Manager")]
    public class ManagerController : ControllerBase
    {
        private readonly IManagerService _managerService;

        public ManagerController(IManagerService managerService)
        {
            _managerService = managerService;
        }

        private int GetUserId()
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        [HttpGet("staff")]
        public async Task<IActionResult> GetStaffInBranch()
        {
            var staffList = await _managerService.GetStaffInBranchAsync(GetUserId());
            return Ok(staffList);
        }

        [HttpGet("lanes")]
        public async Task<IActionResult> GetLanesInBranch([FromQuery] DateTime? date)
        {
            var lanes = await _managerService.GetLanesInBranchAsync(GetUserId(), date);
            return Ok(lanes);
        }

        [HttpGet("lanes/{laneId}/staff")]
        public async Task<IActionResult> GetStaffAssignedToLane(int laneId, [FromQuery] DateTime? date)
        {
            var staffList = await _managerService.GetStaffAssignedToLaneAsync(GetUserId(), laneId, date);
            return Ok(staffList);
        }

        [HttpGet("timeslots")]
        public async Task<IActionResult> GetTimeSlotsInBranch()
        {
            var timeSlots = await _managerService.GetTimeSlotsInBranchAsync(GetUserId());
            return Ok(timeSlots);
        }

        [HttpPost("lanes")]
        public async Task<IActionResult> CreateLane([FromBody] CreateLaneDTO dto)
        {
            var lane = await _managerService.CreateLaneAsync(GetUserId(), dto);
            return CreatedAtAction(nameof(GetLanesInBranch), new { }, lane);
        }

        [HttpPost("timeslots")]
        public async Task<IActionResult> CreateTimeSlot([FromBody] CreateTimeSlotDTO dto)
        {
            var timeSlot = await _managerService.CreateTimeSlotAsync(GetUserId(), dto);
            return CreatedAtAction(nameof(GetTimeSlotsInBranch), new { }, timeSlot);
        }

        [HttpPost("lanes/assign-staff")]
        public async Task<IActionResult> AssignStaffToLane([FromBody] AssignStaffToLaneDTO dto)
        {
            await _managerService.AssignStaffToLaneAsync(GetUserId(), dto);
            return Ok(new { Message = "Staff assigned to lane successfully." });
        }

        [HttpDelete("lanes/{laneId}/staff/{staffId}")]
        public async Task<IActionResult> UnassignStaffFromLane(int laneId, int staffId, [FromQuery] DateTime? date)
        {
            await _managerService.UnassignStaffFromLaneAsync(GetUserId(), laneId, staffId, date);
            return Ok(new { Message = "Staff unassigned from lane successfully." });
        }

        [HttpGet("bookings")]
        public async Task<IActionResult> GetCheckInBookings()
        {
            var bookings = await _managerService.GetCheckInBookingsInBranchAsync(GetUserId());
            return Ok(bookings);
        }

        [HttpPost("bookings/{bookingId}/checkin-assign")]
        public async Task<IActionResult> ConfirmCheckInAndAssignLane(int bookingId, [FromBody] AssignBookingToLaneDTO assignment)
        {
            await _managerService.ConfirmCheckInAndAssignLaneAsync(GetUserId(), bookingId, assignment);
            return Ok(new { Message = "Booking checked in and lanes assigned successfully." });
        }

        [HttpPut("lanes/{laneId}")]
        public async Task<IActionResult> UpdateLane(int laneId, [FromBody] UpdateLaneDTO dto)
        {
            var updatedLane = await _managerService.UpdateLaneAsync(GetUserId(), laneId, dto);
            return Ok(updatedLane);
        }

        [HttpDelete("lanes/{laneId}")]
        public async Task<IActionResult> DeleteLane(int laneId)
        {
            await _managerService.DeleteLaneAsync(GetUserId(), laneId);
            return Ok(new { Message = "Lane deleted successfully." });
        }

        [HttpPut("timeslots/{slotId}")]
        public async Task<IActionResult> UpdateTimeSlot(int slotId, [FromBody] UpdateTimeSlotDTO dto)
        {
            var updatedSlot = await _managerService.UpdateTimeSlotAsync(GetUserId(), slotId, dto);
            return Ok(updatedSlot);
        }

        [HttpDelete("timeslots/{slotId}")]
        public async Task<IActionResult> DeleteTimeSlot(int slotId)
        {
            await _managerService.DeleteTimeSlotAsync(GetUserId(), slotId);
            return Ok(new { Message = "Time slot deleted successfully." });
        }

        [HttpDelete("staff/{userId}")]
        public async Task<IActionResult> DeactivateStaff(int userId)
        {
            await _managerService.DeactivateStaffAsync(GetUserId(), userId);
            return Ok(new { Message = "Staff deactivated successfully." });
        }

        [HttpPost("check-revenue-stimulus")]
        public async Task<IActionResult> CheckRevenueStimulus([FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var result = await _managerService.CheckRevenueStimulusCampaignAsync(GetUserId(), month, year);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("revenue-stimulus/proposals")]
        public async Task<IActionResult> GetPendingProposals()
        {
            var result = await _managerService.GetPendingProposalsAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("revenue-stimulus/proposals/{voucherId}")]
        public async Task<IActionResult> ModifyProposal(int voucherId, [FromBody] ModifyVoucherProposalDTO request)
        {
            var result = await _managerService.ModifyProposalAsync(GetUserId(), voucherId, request);
            return Ok(new { statusCode = 200, message = "Proposal modified successfully.", data = result });
        }

        [HttpPost("revenue-stimulus/proposals/{voucherId}/approve")]
        public async Task<IActionResult> ApproveProposal(int voucherId)
        {
            var result = await _managerService.ApproveProposalAsync(GetUserId(), voucherId);
            return Ok(new { statusCode = 200, message = "Proposal approved and distributed successfully.", data = result });
        }

        [HttpPost("revenue-stimulus/proposals/{voucherId}/reject")]
        public async Task<IActionResult> RejectProposal(int voucherId, [FromBody] RejectVoucherProposalDTO? request)
        {
            var result = await _managerService.RejectProposalAsync(GetUserId(), voucherId, request?.RejectReason);
            return Ok(new { statusCode = 200, message = "Proposal rejected successfully.", data = result });
        }

        [HttpPost("revenue-stimulus/comprehensive-proposals")]
        public async Task<IActionResult> GenerateComprehensiveProposals([FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var result = await _managerService.GenerateComprehensiveStimulusAnalysisAsync(GetUserId(), month, year);
            return Ok(new { statusCode = 200, message = "Comprehensive analysis and proposals generated successfully.", data = result });
        }

        [HttpPost("branch-overload/scan-and-notify-relocation")]
        public async Task<IActionResult> ScanAndNotifyRelocation()
        {
            var result = await _managerService.ScanAndNotifyRelocationAsync(GetUserId());
            return Ok(new { statusCode = 200, message = "Overload scan completed. OverloadSuggestion rows created and FCM notifications sent.", data = result });
        }
    }
}
