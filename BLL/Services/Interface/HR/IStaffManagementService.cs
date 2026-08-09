using AutoWashPro.BLL.DTOs;
namespace AutoWashPro.BLL.Services
{
    public interface IStaffManagementService
    {
        Task<List<StaffResponseDTO>> GetStaffsAsync(string? keyword, string? role, string? status);
        Task<List<StaffResponseDTO>> GetStaffsByRoleAsync(string role, string? keyword, string? status);
        Task<StaffResponseDTO> GetStaffByRoleAsync(int staffUserId, string role);
        Task<StaffResponseDTO> CreateStaffAsync(CreateStaffDTO request);
        Task<StaffResponseDTO> CreateStaffWithRoleAsync(CreateStaffDTO request, string role);
        Task<StaffResponseDTO> UpdateStaffAsync(int staffUserId, UpdateStaffDTO request);
        Task<StaffResponseDTO> UpdateStaffByRoleAsync(int staffUserId, string role, UpdateStaffDTO request);
        Task<bool> UpdateStaffStatusAsync(int staffUserId, string status);
        Task<bool> SoftDeleteStaffByRoleAsync(int staffUserId, string role);
        Task<List<WorkShiftResponseDTO>> GetWorkShiftsAsync(bool includeInactive);
        Task<WorkShiftResponseDTO> CreateWorkShiftAsync(CreateWorkShiftDTO request);
        Task<WorkShiftResponseDTO> UpdateWorkShiftAsync(int workShiftId, UpdateWorkShiftDTO request);
        Task<bool> DeleteWorkShiftAsync(int workShiftId);
        Task<List<ShiftAssignmentResponseDTO>> GetShiftAssignmentsAsync(DateTime? fromDate, DateTime? toDate, int? staffUserId);
        Task<List<ShiftAssignmentResponseDTO>> GetMyShiftAssignmentsAsync(int staffUserId, DateTime? fromDate, DateTime? toDate);
        Task<ShiftAssignmentResponseDTO> CreateShiftAssignmentAsync(CreateShiftAssignmentDTO request);
        Task<ShiftAssignmentResponseDTO> UpdateShiftAssignmentAsync(int assignmentId, UpdateShiftAssignmentDTO request);
        Task<bool> DeleteShiftAssignmentAsync(int assignmentId);
        Task<List<OvertimeRequestResponseDTO>> GetOvertimeRequestsAsync(string? status);
        Task<List<OvertimeRequestResponseDTO>> GetMyOvertimeRequestsAsync(int staffUserId);
        Task<OvertimeRequestResponseDTO> CreateOvertimeRequestAsync(int staffUserId, CreateOvertimeRequestDTO request);
        Task<OvertimeRequestResponseDTO> ReviewOvertimeRequestAsync(int requestId, int managerUserId, ReviewRequestDTO request);
        Task<List<ShiftSwapRequestResponseDTO>> GetShiftSwapRequestsAsync(string? status);
        Task<List<ShiftSwapRequestResponseDTO>> GetMyShiftSwapRequestsAsync(int staffUserId);
        Task<ShiftSwapRequestResponseDTO> CreateShiftSwapRequestAsync(int staffUserId, CreateShiftSwapRequestDTO request);
        Task<ShiftSwapRequestResponseDTO> ReviewShiftSwapRequestAsync(int requestId, int managerUserId, ReviewRequestDTO request);
    }
}
