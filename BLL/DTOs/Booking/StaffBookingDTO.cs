using System;
using System.Collections.Generic;

namespace AutoWashPro.BLL.DTOs
{
    public class StaffBookingDTO
    {
        public int BookingId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public List<string> ServiceNames { get; set; } = new List<string>();
        public string VehicleTypeName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? PaymentMethod { get; set; }
        public decimal FinalAmount { get; set; }
        public string? OrderCode { get; set; }
        public DateTime? ProcessingStartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int? ActualDurationMinutes { get; set; }
        public string? CustomerTierName { get; set; }
        public int? CustomerTierPoints { get; set; }
    }
}
