using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Business
{
    public class CheckBusinessSlotsRequestDTO
    {
        public int BranchId { get; set; }
        public int FleetVehicleId { get; set; }
        public List<int> ServiceIds { get; set; } = new();
        public DateTime TargetDate { get; set; }
        public int? VehicleCount { get; set; }
    }
}
