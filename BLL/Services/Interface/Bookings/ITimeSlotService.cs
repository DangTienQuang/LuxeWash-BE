using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
namespace AutoWashPro.BLL.Services
{
    public interface ITimeSlotService
    {
        Task<List<TimeSlotAdminResponseDTO>> GetAllTimeSlotsAsync(int? branchId = null);
        Task<TimeSlotAdminResponseDTO> CreateTimeSlotAsync(CreateTimeSlotDTO request);
        Task<TimeSlotAdminResponseDTO> UpdateTimeSlotAsync(int slotId, UpdateTimeSlotDTO request);
        Task<bool> DeleteTimeSlotAsync(int slotId);
    }
}
