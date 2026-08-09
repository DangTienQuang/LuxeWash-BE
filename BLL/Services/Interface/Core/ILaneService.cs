using AutoWashPro.BLL.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface ILaneService
    {
        Task<List<LaneDTO>> GetAllLanesAsync(int? branchId = null);
        Task<LaneDTO> GetLaneByIdAsync(int laneId);
        Task<LaneDTO> CreateLaneAsync(CreateLaneDTO createDto);
        Task<LaneDTO> UpdateLaneAsync(int laneId, UpdateLaneDTO updateDto);
    }
}
