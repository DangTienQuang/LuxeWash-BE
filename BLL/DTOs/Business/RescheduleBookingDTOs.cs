using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs.Business
{
    public class RescheduleBusinessBookingDTO
    {
        public int BookingId { get; set; }
        public DateTime NewScheduledDate { get; set; }
        public int NewSlotId { get; set; }
    }

    public class RescheduleBusinessResponseDTO
    {
        public int BookingId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public DateTime OldScheduledTime { get; set; }
        public DateTime NewScheduledTime { get; set; }
        public int LaneId { get; set; }
        public string LaneName { get; set; } = string.Empty;
        public DateTime EstimatedStart { get; set; }
        public DateTime EstimatedEnd { get; set; }
    }
}
