using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Business
{
    public class CreateBusinessBookingDTO
    {
        public List<VehicleBookingItemDTO> Vehicles { get; set; } = new();
        public int BranchId { get; set; }
        public int SlotId { get; set; }
        public DateTime ScheduledTime { get; set; }
    }
    public class VehicleBookingItemDTO
    {
        public int FleetVehicleId { get; set; }
        public List<int> ServiceIds { get; set; } = new();
    }
}
