using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IIncidentService
    {
        Task<PreviewIncidentResponseDTO> PreviewIncidentImpactAsync(int managerUserId, PreviewIncidentRequestDTO request);
        Task<long> CreateIncidentAsync(int managerUserId, CreateIncidentRequestDTO request);
        Task ExtendIncidentAsync(int managerUserId, long incidentId, ExtendIncidentRequestDTO request);
        Task ResolveIncidentAsync(int managerUserId, long incidentId);
    }
}
