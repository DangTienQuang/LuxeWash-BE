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
        public int FleetVehicleId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string? DriverName { get; set; }
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = null!;
    }
    public class StartFleetWashDTO
    {
        public int LaneId { get; set; }
    }
}
