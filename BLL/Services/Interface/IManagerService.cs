using AutoWashPro.BLL.DTOs;
using BLL.DTOs.Business;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public interface IManagerService
    {
        Task<List<ManagerStaffDTO>> GetStaffInBranchAsync(int managerUserId);
        Task<bool> AssignStaffToLaneAsync(int managerUserId, AssignStaffToLaneDTO assignDto);
        Task<bool> UnassignStaffFromLaneAsync(int managerUserId, int laneId, int staffId, System.DateTime? date = null);
        Task<List<ManagerBookingListDTO>> GetCheckInBookingsInBranchAsync(int managerUserId);
        Task<bool> ConfirmCheckInAndAssignLaneAsync(int managerUserId, int bookingId, AssignBookingToLaneDTO assignment);
        Task<List<LaneStaffAssignmentDTO>> GetLanesInBranchAsync(int managerUserId, System.DateTime? date = null);
        Task<List<ManagerStaffDTO>> GetStaffAssignedToLaneAsync(int managerUserId, int laneId, System.DateTime? date = null);
        Task<List<TimeSlotAdminResponseDTO>> GetTimeSlotsInBranchAsync(int managerUserId);
        Task<LaneDTO> CreateLaneAsync(int managerUserId, CreateLaneDTO request);
        Task<TimeSlotAdminResponseDTO> CreateTimeSlotAsync(int managerUserId, CreateTimeSlotDTO request);
        Task<LaneDTO> UpdateLaneAsync(int managerUserId, int laneId, UpdateLaneDTO request);
        Task<bool> DeleteLaneAsync(int managerUserId, int laneId);
        Task<TimeSlotAdminResponseDTO> UpdateTimeSlotAsync(int managerUserId, int slotId, UpdateTimeSlotDTO request);
        Task<bool> DeleteTimeSlotAsync(int managerUserId, int slotId);
        Task<bool> DeactivateStaffAsync(int managerUserId, int staffUserId);
        Task<MonthlyRevenueCampaignResultDTO> CheckRevenueStimulusCampaignAsync(int managerUserId, int? month = null, int? year = null);
        Task<List<VoucherProposalDTO>> GetPendingProposalsAsync(int managerUserId);
        Task<VoucherProposalDTO> ModifyProposalAsync(int managerUserId, int voucherId, ModifyVoucherProposalDTO dto);
        Task<MonthlyRevenueCampaignResultDTO> ApproveProposalAsync(int managerUserId, int voucherId);
        Task<bool> RejectProposalAsync(int managerUserId, int voucherId, string? rejectReason);
        Task<BranchComprehensiveStimulusDTO> GenerateComprehensiveStimulusAnalysisAsync(int managerUserId, int? month = null, int? year = null);
    }
}
