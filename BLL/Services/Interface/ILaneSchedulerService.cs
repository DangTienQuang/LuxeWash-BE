using BLL.DTOs.Business;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ILaneSchedulerService
    {
        Task<Dictionary<int, DateTime>> GetLaneProjectedFreeTimesAsync(int branchId, DateTime slotStart, bool isBusinessLane = false);

        Task<int> GetBestAvailableLaneAsync(int branchId, bool isBusinessLane = false);

        Task<int> AssignBestAvailableLaneAtomicAsync(int bookingId);

        Task<bool> AssignNextVehicleInQueueAsync(int laneId);

        Task<LaneScheduleResult> ScheduleFleetAsync(int branchId, DateTime slotStart, TimeSpan slotDuration, List<VehicleScheduleRequest> vehicles);

    }
}
