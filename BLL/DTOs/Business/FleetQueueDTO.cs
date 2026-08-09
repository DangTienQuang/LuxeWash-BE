using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class FleetQueueDTO
    {
        public int Position { get; set; }
        public int FleetWashLogId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string? DriverName { get; set; }
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = null!;
    }
}
