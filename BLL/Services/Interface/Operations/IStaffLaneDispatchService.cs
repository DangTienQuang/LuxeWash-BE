using AutoWashPro.BLL.DTOs.Operations;

namespace AutoWashPro.BLL.Services.Operations
{
    public interface IStaffLaneDispatchService
    {
        Task<StaffLaneDispatchResponseDTO> CreateDispatchAsync(int managerUserId, CreateStaffLaneDispatchDTO request);
        Task<List<StaffLaneDispatchResponseDTO>> GetManagerDispatchesAsync(
            int managerUserId,
            string? status = null,
            DateTime? from = null,
            DateTime? to = null);
        Task CancelDispatchAsync(int managerUserId, int dispatchId);
        Task<List<StaffLaneDispatchResponseDTO>> GetActiveDispatchesForStaffAsync(int managerUserId, int staffUserId);
        Task<List<StaffLaneDispatchResponseDTO>> GetStaffDispatchesAsync(int staffUserId, string? status = null);
        Task<StaffLaneDispatchResponseDTO> AcknowledgeDispatchAsync(int staffUserId, int dispatchId);
        Task<StaffLaneDispatchResponseDTO> CompleteDispatchAsync(int staffUserId, int dispatchId, CompleteStaffLaneDispatchDTO request);
    }
}
