using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs.Operations;

namespace AutoWashPro.BLL.Services.Operations
{
    public enum BarrierPublishResult
    {
        Published,
        SkippedNoFirebase,
        Failed
    }

    public interface ILaneDisplayPublisherService
    {
        Task PublishEventAsync(LaneDisplayEventDTO eventDto);
        Task PublishClearAsync(int branchId, int? laneId, string? laneName);
        Task<LaneDisplayLatestResponseDTO> GetLatestStateAsync(int branchId);
        Task<BarrierPublishResult> PublishBarrierCommandAsync(int branchId, string licensePlate, string laneName);
        Task<BarrierPublishResult> PublishBarrierCommandRawAsync(int branchId, string jsonPayload);
    }
}
