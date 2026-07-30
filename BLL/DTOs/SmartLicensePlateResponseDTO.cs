using System;
using System.Collections.Generic;

namespace AutoWashPro.BLL.DTOs
{
    public class SmartLicensePlateResponseDTO
    {
        public string CustomerType { get; set; } = "WalkIn";
        public object? Data { get; set; }

        // VIP info - populated for PreBooked, CheckedIn, Processing, and registered WalkIn
        public string? CustomerTierName { get; set; }
        public int? CustomerTierPoints { get; set; }
        public bool IsVip { get; set; }
    }
}
