using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class FleetWashLogDTO
    {
        public int FleetWashLogId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
