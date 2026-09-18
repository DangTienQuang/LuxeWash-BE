using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IIncidentCustomerService
    {
        Task<IncidentDecisionResponseDTO> ProcessIncidentDecisionAsync(int userId, int bookingId, IncidentDecisionRequestDTO request);
        Task<IncidentOptionsResponseDTO?> GetIncidentOptionsAsync(int userId, int bookingId);
    }
}
