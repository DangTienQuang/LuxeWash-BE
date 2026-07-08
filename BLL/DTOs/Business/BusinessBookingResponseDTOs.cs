using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs.Business
{
    public class BusinessBookingResponseDTO
    {
        public int BookingId { get; set; }

        public string LicensePlate { get; set; } = null!;

        public decimal OriginalPrice { get; set; }

        public decimal FinalAmount { get; set; }

        public string Status { get; set; } = null!;
    }

    public class BusinessBookingDetailDTO
    {
        public int BookingId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public DateTime ScheduledTime { get; set; }
        public string Status { get; set; } = null!;
        public decimal OriginalPrice { get; set; }
        public decimal FinalAmount { get; set; }
        public List<string> Services { get; set; } = [];
        public bool IsBusinessLane { get; set; }
    }

    public class MultiVehicleBookingResponseDTO
    {
        public int BookingGroupId { get; set; }
        public int TotalVehicles { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<VehicleBookingSummaryDTO> Vehicles { get; set; } = new();
    }

    public class VehicleBookingSummaryDTO
    {
        public int BookingId { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public int LaneId { get; set; }
        public string LaneName { get; set; } = string.Empty;
        public DateTime EstimatedStart { get; set; }
        public DateTime EstimatedEnd { get; set; }
        public decimal Amount { get; set; }
    }

    public class TimeSlotResponseDTO
    {
        public int SlotId { get; set; }
        public string TimeRange { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int? EstimatedLastEndMinutesIntoSlot { get; set; }
        public List<VehicleSlotProjectionDTO> VehicleProjections { get; set; } = new();
    }

    public class VehicleSlotProjectionDTO
    {
        public int FleetVehicleId { get; set; }
        public DateTime EstimatedStart { get; set; }
        public DateTime EstimatedEnd { get; set; }
        public string LaneName { get; set; } = string.Empty;
    }
}
