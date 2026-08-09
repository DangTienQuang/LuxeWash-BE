using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class FleetVehicleDTO
    {
        public int FleetVehicleId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string VehicleType { get; set; } = null!;
        public string VehicleTypeName { get; set; } = null!;
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public string? DriverName { get; set; }
        public string? EmployeeId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
    public class CurrentFleetVehicleDTO
    {
        public int FleetWashLogId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string? DriverName { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CheckInTime { get; set; }
    }
    public class VehicleStatementDTO
    {
        public int FleetVehicleId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public int WashCount { get; set; }
        public decimal TotalCost { get; set; }
    }
}
