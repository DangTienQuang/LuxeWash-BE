using System;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs.Operations
{
    public class CreateStaffLaneDispatchDTO
    {
        [Required]
        public int LaneId { get; set; }

        [Required]
        public int StaffUserId { get; set; }

        [Required]
        [MaxLength(30)]
        public string Reason { get; set; } = "incident";

        [MaxLength(500)]
        public string? Note { get; set; }

        public int? RelatedIncidentId { get; set; }
    }

    public class CompleteStaffLaneDispatchDTO
    {
        [MaxLength(500)]
        public string? NoteFromStaff { get; set; }

        [Required]
        [MaxLength(30)]
        public string IncidentResolution { get; set; } = null!;
    }

    public class StaffLaneDispatchResponseDTO
    {
        public int DispatchId { get; set; }
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public int BranchId { get; set; }
        public int DispatchedByUserId { get; set; }
        public string DispatchedByFullName { get; set; } = null!;
        public int StaffUserId { get; set; }
        public string StaffFullName { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public string? Note { get; set; }
        public DateTime DispatchedAt { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? NoteFromStaff { get; set; }
        public string? IncidentResolution { get; set; }
        public int? RelatedIncidentId { get; set; }
    }
}
