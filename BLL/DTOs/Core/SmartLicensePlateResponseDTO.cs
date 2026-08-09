using System;
using System.Collections.Generic;
namespace AutoWashPro.BLL.DTOs
{
    public class SmartLicensePlateResponseDTO
    {
        public string CustomerType { get; set; } = "WalkIn";
        public object? Data { get; set; }
        public string? CustomerTierName { get; set; }
        public int? CustomerTierPoints { get; set; }
        public bool IsVip { get; set; }
    }
}
