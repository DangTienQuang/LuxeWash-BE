using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class PreviewIncidentRequestDTO
    {
        [Required]
        public int BranchId { get; set; }
        
        [Required]
        public string Type { get; set; } = null!; // "LaneFailure", "PowerOutage", "WaterOutage", "Other"

        [Required]
        public string Scope { get; set; } = null!; // "SelectedLanes", "WholeBranch"

        public List<int>? LaneIds { get; set; }

        [Required]
        public DateTime EstimatedEndAtVn { get; set; }
    }

    public class PreviewIncidentResponseDTO
    {
        public int AffectedBookingsCount { get; set; }
        public int TotalCapacityLoss { get; set; }
        public List<AffectedBookingSummaryDTO> AffectedBookings { get; set; } = new List<AffectedBookingSummaryDTO>();
    }

    public class AffectedBookingSummaryDTO
    {
        public int BookingId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string ScheduledTime { get; set; } = null!;
        public int CapacityWeight { get; set; }
    }

    public class CreateIncidentRequestDTO : PreviewIncidentRequestDTO
    {
        [Required]
        public string Reason { get; set; } = null!;
        public int? ExpectedAffectedCount { get; set; }
    }

    public class ExtendIncidentRequestDTO
    {
        [Required]
        public DateTime NewEstimatedEndAtVn { get; set; }
        public string? Note { get; set; }
    }

    public class BranchIncidentDTO
    {
        public long IncidentId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Scope { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime EstimatedEndAtVn { get; set; }
        public DateTime CreatedAtVn { get; set; }
        public DateTime? ResolvedAtVn { get; set; }
        public int CreatedByUserId { get; set; }
        public List<int>? LaneIds { get; set; }
    }

    public class IncidentAffectedBookingDTO
    {
        public long AffectedBookingId { get; set; }
        public int BookingId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string ScheduledTime { get; set; } = null!;
        public string CustomerAction { get; set; } = null!;
        public string SystemResolution { get; set; } = null!;
        public DateTime CustomerDeadlineVn { get; set; }
        public string? AlternativeBranchId { get; set; }
        public string? AlternativeTimeSlot { get; set; }
    }
}
namespace AutoWashPro.BLL.DTOs
{
    public class IncidentPagedResponseDTO
    {
        public List<BranchIncidentDTO> Items { get; set; } = new List<BranchIncidentDTO>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
namespace AutoWashPro.BLL.DTOs
{
    public class IncidentOptionsResponseDTO
    {
        public long CaseId { get; set; }
        public long IncidentId { get; set; }
        public string CaseStatus { get; set; } = null!;
        public object? OriginalBooking { get; set; } 
        public string Reason { get; set; } = null!;
        public string Eta { get; set; } = null!;
        public string ResponseDeadlineAt { get; set; } = null!;
        public List<string> AllowedActions { get; set; } = new List<string>();
        public List<IncidentAlternativeDTO> Alternatives { get; set; } = new List<IncidentAlternativeDTO>();
        public RefundPreviewDTO RefundPreview { get; set; } = new RefundPreviewDTO();
        public VoucherTermsDTO VoucherTerms { get; set; } = new VoucherTermsDTO();
        public int Version { get; set; }
    }

    public class IncidentAlternativeDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public int SlotId { get; set; }
        public string StartAt { get; set; } = null!;
        public string EndAt { get; set; } = null!;
        public double? DistanceKm { get; set; }
        public int AvailableWeight { get; set; }
        public decimal PriceDifferenceCharged { get; set; } = 0;
    }

    public class RefundPreviewDTO
    {
        public decimal Amount { get; set; }
        public string Destination { get; set; } = "Wallet";
        public int PointsRestored { get; set; }
        public bool OriginalVoucherRestored { get; set; }
    }

    public class VoucherTermsDTO
    {
        public int DiscountPercent { get; set; } = 20;
        public string Code { get; set; } = "INCIDENT_COMP_20";
    }

    public class IncidentDecisionRequestDTO
    {
        [Required]
        public long IncidentId { get; set; }
        [Required]
        public long CaseId { get; set; }
        [Required]
        public int ExpectedVersion { get; set; }
        [Required]
        public string Decision { get; set; } = null!; // "Cancel", "Transfer", "Keep"
        public int? TargetBranchId { get; set; }
        public int? TargetSlotId { get; set; }
    }

    public class IncidentDecisionResponseDTO
    {
        public string Decision { get; set; } = null!;
        public object? Booking { get; set; }
        public RefundPreviewDTO? Refund { get; set; }
        public CompensationVoucherDTO? CompensationVoucher { get; set; }
        public string CaseStatus { get; set; } = null!;
    }

    public class CompensationVoucherDTO
    {
        public int VoucherId { get; set; }
        public int DiscountPercent { get; set; }
        public string ExpiresAt { get; set; } = null!;
    }
}
