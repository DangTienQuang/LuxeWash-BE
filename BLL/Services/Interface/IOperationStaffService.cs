using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Operations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public interface IOperationStaffService
    {
        Task<StaffLaneTaskDTO?> GetTodayLaneAssignmentAsync(int staffUserId, System.DateTime? date = null);
        Task<List<StaffBookingDTO>> GetAssignedBookingsAsync(int staffUserId, System.DateTime? date = null);
        Task<bool> UpdateBookingStatusAsync(int staffUserId, int bookingId, string newStatus);
        Task<GateCheckInResult> CheckInBookingAsync(int staffUserId, int bookingId);
        Task<List<LaneOccupancyDTO>> GetActiveLaneOccupanciesAsync(int staffUserId);
        Task<bool> SwapShiftByPhoneAsync(int currentStaffId, SwapLaneByPhoneDTO dto);
    }
}
