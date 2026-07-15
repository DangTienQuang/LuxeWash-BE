using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class TimeSlotResponseDTO
    {
        public int SlotId { get; set; }
        public required string TimeRange { get; set; } 
        public bool IsAvailable { get; set; }
        public string? Reason { get; set; }
    }

    public class CompatibilityDTO
    {
        public bool IsCompatible { get; set; }
        public string? Message { get; set; }
        public int RemainingCapacity { get; set; }
        public int TotalCapacityWeight { get; set; }
        public int MaxCapacityOfSlot { get; set; }
    }

    public class CheckCompatibilityRequestDTO
    {
        [Required]
        public int BranchId { get; set; }

        [Required]
        public int SlotId { get; set; }

        [Required]
        public DateTime TargetDate { get; set; }

        [Required]
        [MaxLength(20)]
        public required string LicensePlate { get; set; }

        public int? VehicleId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Please select at least 1 service.")]
        public required List<int> ServiceIds { get; set; }
    }
    public class UpdateBookingStatusByPlateDTO
    {
        [Required(ErrorMessage = "License plate is required.")]
        [MinLength(1)]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "License plate is invalid.")]
        public string LicensePlate { get; set; } = string.Empty;

        [Required(ErrorMessage = "Status is required.")]
        public string NewStatus { get; set; } = string.Empty;
    }

    public class CheckAvailableSlotsRequestDTO
    {
        [Required]
        public int BranchId { get; set; }

        [Required]
        public DateTime TargetDate { get; set; }

        [Required]
        public int VehicleTypeId { get; set; }

        [Required]
        public List<int> ServiceIds { get; set; } = new List<int>();
    }
    public class CreateBookingDTO
    {
        [Required]
        public int BranchId { get; set; }

        public int? VehicleId { get; set; }

        [Required]
        [MaxLength(20)]
        public required string LicensePlate { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Please select at least 1 service.")]
        public required List<int> ServiceIds { get; set; }

        [Required]
        public DateTime ScheduledDate { get; set; }

        [Required]
        public int SlotId { get; set; }

        public int PointsToUse { get; set; } = 0;

        public int? VoucherId { get; set; }

        public string PaymentMethod { get; set; } = "Wallet";
    }

    public class CreateBookingPaymentLinkDTO
    {
        [Required]
        [MaxLength(2000)]
        public required string CancelUrl { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string ReturnUrl { get; set; }
    }

    public class RescheduleBookingDTO
    {
        [Required]
        public DateTime NewScheduledDate { get; set; }

        [Required]
        public int NewSlotId { get; set; }
    }

    public class BookingPaymentLinkResponseDTO
    {
        public required string PaymentUrl { get; set; }
        public required string OrderCode { get; set; }
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
    }

    public class CreateWalkInBookingDTO
    {
        [Required]
        public int BranchId { get; set; }

        public int? VehicleId { get; set; }

        public int? VehicleTypeId { get; set; }

        [Required]
        [MaxLength(20)]
        public required string LicensePlate { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Please select at least 1 service.")]
        public required List<int> ServiceIds { get; set; }

        public int UserId { get; set; }

        public int PointsToUse { get; set; } = 0;

        public int? VoucherId { get; set; }

        public string? PaymentMethod { get; set; }

        public string? ReturnUrl { get; set; }

        public string? CancelUrl { get; set; }

        public bool ForceOverrideCapacity { get; set; } = false;
    }

    public class WalkInBookingResponseDTO : BookingResponseDTO
    {
        public string? PaymentUrl { get; set; }
    }

    public class BookingResponseDTO
    {
        public int BookingId { get; set; }
        public required string LicensePlate { get; set; }
        public required List<string> ServiceNames { get; set; }
        public DateTime ScheduledTime { get; set; }
        public required string Status { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal PointDiscountAmount { get; set; }
        public decimal VoucherDiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public DateTime? ProcessingStartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int? ActualDurationMinutes { get; set; }
    }

    public class AdminBookingResponseDTO : BookingResponseDTO
    {
        public string PaymentStatus { get; set; } = "Unpaid";
    }

    public class BookingPaymentStatusDTO
    {
        public int BookingId { get; set; }
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? PaymentMethod { get; set; }
        public string? OrderCode { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class UpdateVehicleConditionDTO
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public DAL.Entities.VehicleCondition Condition { get; set; }

        public int? ActualVehicleTypeId { get; set; }
    }
}

namespace AutoWashPro.BLL.DTOs
{
    public class ForceCancelRequestDTO
    {
        [Required]
        public int BranchId { get; set; }

        public int? TimeSlotId { get; set; }

        public DateTime? AffectedDate { get; set; }

        [Required(ErrorMessage = "Reason is required to notify customers.")]
        public required string Reason { get; set; }
    }
}

