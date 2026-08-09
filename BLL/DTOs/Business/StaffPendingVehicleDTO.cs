using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class StaffPendingVehicleDTO
    {
        public int FleetVehicleId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string VehicleTypeName { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public int BusinessProfileId { get; set; }
        public int? FleetImportBatchId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
