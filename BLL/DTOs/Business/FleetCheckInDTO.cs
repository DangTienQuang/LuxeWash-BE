using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class FleetCheckInDTO
    {
        public int BookingId { get; set; }
    }
    public class FleetCheckInResponseDTO
    {
        public int FleetWashLogId { get; set; }
        public int? BookingId { get; set; }
        public int FleetVehicleId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string? DriverName { get; set; }
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = null!;
        public bool IsWaiting { get; set; }
        public int? LaneId { get; set; }
        public string? LaneName { get; set; }
    }
    public class StartFleetWashDTO
    {
        public int LaneId { get; set; }
    }
}
