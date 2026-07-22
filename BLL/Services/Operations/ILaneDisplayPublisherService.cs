using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs.Operations;

namespace AutoWashPro.BLL.Services.Operations
{
    public interface ILaneDisplayPublisherService
    {
        Task PublishEventAsync(LaneDisplayEventDTO eventDto);
        Task PublishClearAsync(int branchId, int laneId, string laneName);
        Task<List<LaneDisplayLatestStateDTO>> GetLatestStateAsync(int branchId);
    }
}
