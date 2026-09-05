using System;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs.Operations
{
    public class CreateLaneIncidentDTO
    {
        [Required]
        public int LaneId { get; set; }

        [Required]
        [MaxLength(30)]
        public string IssueType { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = null!;
    }

    public class ResolveLaneIncidentDTO
    {
        public bool Reactivated { get; set; }

        [MaxLength(1000)]
        public string? ResolutionNote { get; set; }
    }

    public class ReactivateLaneDTO
    {
        [MaxLength(1000)]
        public string? ResolutionNote { get; set; }
    }

    public class LaneIncidentResponseDTO
    {
        public int IncidentId { get; set; }
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public int BranchId { get; set; }
        public int ReportedByUserId { get; set; }
        public string ReportedByFullName { get; set; } = null!;
        public string IssueType { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime ReportedAt { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedByUserId { get; set; }
        public string? ResolutionNote { get; set; }
        public bool LaneReactivated { get; set; }
    }

    public class LaneIncidentDetailDTO : LaneIncidentResponseDTO
    {
        public List<StaffLaneDispatchResponseDTO> Dispatches { get; set; } = new();
    }

    public class ManagerLaneStatusDTO
    {
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public int BranchId { get; set; }
        public bool IsActive { get; set; }
        public bool IsBusinessLane { get; set; }
        public bool IsVipLane { get; set; }
        public bool HasOpenIncident { get; set; }
        public string? DeactivationReason { get; set; }
        public int? DeactivatedByUserId { get; set; }
        public DateTime? DeactivatedAt { get; set; }
        public LaneIncidentResponseDTO? ActiveIncident { get; set; }
    }
}
