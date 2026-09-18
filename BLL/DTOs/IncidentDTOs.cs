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
    }

    public class ExtendIncidentRequestDTO
    {
        [Required]
        public DateTime NewEstimatedEndAtVn { get; set; }
        public string? Note { get; set; }
    }
}
