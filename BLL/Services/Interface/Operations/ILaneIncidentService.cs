using AutoWashPro.BLL.DTOs.Operations;

namespace AutoWashPro.BLL.Services.Operations
{
    public interface ILaneIncidentService
    {
        Task<List<ManagerLaneStatusDTO>> GetManagerLaneStatusesAsync(int managerUserId);
        Task<List<LaneIncidentResponseDTO>> GetManagerIncidentsAsync(int managerUserId, string? status = null);
        Task<LaneIncidentDetailDTO> GetManagerIncidentAsync(int managerUserId, int incidentId);
        Task<LaneIncidentResponseDTO> ResolveIncidentAsync(int managerUserId, int incidentId, ResolveLaneIncidentDTO request);
        Task DeleteIncidentAsync(int managerUserId, int incidentId);
        Task<ManagerLaneStatusDTO> ReactivateLaneAsync(int managerUserId, int laneId, ReactivateLaneDTO request);
        Task<LaneIncidentResponseDTO> CreateStaffIncidentAsync(int staffUserId, CreateLaneIncidentDTO request);
        Task<List<LaneIncidentResponseDTO>> GetStaffReportsAsync(int staffUserId);
        Task<List<ManagerLaneStatusDTO>> GetStaffLanesAsync(int staffUserId);
    }
}
