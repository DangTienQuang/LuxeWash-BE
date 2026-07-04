using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IBookingMaterialUsageService
    {
        Task ConsumeForCompletedBookingAsync(int bookingId, int? actorUserId = null);
        Task<ExtraMaterialUsageRequestDTO> CreateExtraUsageRequestAsync(int bookingId, int actorUserId, ReportExtraMaterialUsageDTO dto);
        Task<List<ExtraMaterialUsageRequestDTO>> GetManagerExtraUsageRequestsAsync(int managerUserId, string? status = null);
        Task<ExtraMaterialUsageRequestDTO> ApproveExtraUsageRequestAsync(int managerUserId, int requestId, ReviewExtraMaterialUsageRequestDTO dto);
        Task<ExtraMaterialUsageRequestDTO> RejectExtraUsageRequestAsync(int managerUserId, int requestId, ReviewExtraMaterialUsageRequestDTO dto);
    }
}
