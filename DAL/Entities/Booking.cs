using DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User? User { get; set; }

        [MaxLength(255)]
        public string? FallbackQrCode { get; set; }

        [Required]
        public DateTime ScheduledTime { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [Required]
        public int BranchId { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        // Added from BookingDetail
        public int? VehicleId { get; set; }
        [ForeignKey("VehicleId")]
        public Vehicle? Vehicle { get; set; }

        [Required]
        [MaxLength(20)]
        public string LicensePlate { get; set; } = null!;

        public int CapacityWeight { get; set; }
        public VehicleCondition VehicleCondition { get; set; } = VehicleCondition.Clean;

        public int? ActualVehicleTypeId { get; set; }
        [ForeignKey("ActualVehicleTypeId")]
        public VehicleType? ActualVehicleType { get; set; }

        public decimal MismatchSurcharge { get; set; } = 0;

        public int? ProcessingLaneId { get; set; }
        [ForeignKey("ProcessingLaneId")]
        public Lane? ProcessingLane { get; set; }

        public int? ProcessingStaffId { get; set; }
        [ForeignKey("ProcessingStaffId")]
        public User? ProcessingStaff { get; set; }

        public ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();

        [Required]
        public decimal OriginalPrice { get; set; }
        [ForeignKey("BusinessProfile")]
        public int? BusinessProfileId { get; set; }

        public string BookingType { get; set; } = "Personal";

        public BusinessProfile? BusinessProfile { get; set; }
        public int PointsUsed { get; set; } = 0;

        public decimal PointDiscountAmount { get; set; } = 0;

        public int? AppliedVoucherId { get; set; }

        public decimal VoucherDiscountAmount { get; set; } = 0;

        [Required]
        public decimal FinalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public DateTime? ProcessingStartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int? ActualDurationMinutes { get; set; }

        public int? FleetVehicleId { get; set; }

        [ForeignKey(nameof(FleetVehicleId))]
        public FleetVehicle? FleetVehicle { get; set; }
    }
}
