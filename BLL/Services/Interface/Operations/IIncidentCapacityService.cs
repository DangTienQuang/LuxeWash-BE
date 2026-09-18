using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IIncidentCapacityService
    {
        Task<EffectiveSlotCapacityResult> GetEffectiveSlotCapacityAsync(int branchId, DateTime date, int slotId, BookingContextDTO bookingContext, DateTime nowVn, AutoWashPro.DAL.Entities.BranchIncident? simulatedIncident = null);
    }

    public class EffectiveSlotCapacityResult
    {
        public int BaseCapacity { get; set; }
        public int EffectiveCapacity { get; set; }
        public int BookedWeight { get; set; }
        public int AvailableWeight { get; set; }
        public string? ClosedReason { get; set; }
        public List<long> IncidentIds { get; set; } = new List<long>();
        public List<int> EligibleLaneIds { get; set; } = new List<int>();
        public int CapacityVersion { get; set; }
    }

    public class BookingContextDTO
    {
        public bool IsBusiness { get; set; }
        public bool IsVipEligible { get; set; }
        public int? VehicleTypeId { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
        public int CapacityWeight { get; set; }
    }
}
