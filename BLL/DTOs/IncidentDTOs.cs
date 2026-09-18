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
    public class IncidentAffectedBookingMobileDTO
    {
        public long AffectedBookingId { get; set; }
        public int BookingId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CustomerDeadlineVn { get; set; }
        public string BranchName { get; set; } = null!;
        public string ScheduledTime { get; set; } = null!;
        public string LicensePlate { get; set; } = null!;
        
        // Options available for transfer
        public List<AlternativeBranchSlotDTO> AlternativeOptions { get; set; } = new List<AlternativeBranchSlotDTO>();
    }

    public class AlternativeBranchSlotDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public double DistanceKm { get; set; }
        public List<AvailableSlotDTO> Slots { get; set; } = new List<AvailableSlotDTO>();
    }

    public class AvailableSlotDTO
    {
        public int SlotId { get; set; }
        public string Time { get; set; } = null!; // e.g. "08:00 - 08:30"
    }
}
