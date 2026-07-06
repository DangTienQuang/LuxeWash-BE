using System;
using System.Collections.Generic;

namespace AutoWashPro.BLL.DTOs
{
    public class ManagerBookingListDTO
    {
        public int BookingId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime ScheduledTime { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string LicensePlate { get; set; } = null!;
        public List<string> ServiceNames { get; set; } = new List<string>();
        public int? ProcessingLaneId { get; set; }
        public string? ProcessingLaneName { get; set; }
        public bool IsBusinessLane { get; set; }
        public int? ProcessingStaffId { get; set; }
        public string? ProcessingStaffName { get; set; }
        public DateTime? ProcessingStartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int? ActualDurationMinutes { get; set; }
    }
}
