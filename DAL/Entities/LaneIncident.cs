using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class LaneIncident
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LaneId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        public int ReportedByUserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReportedByFullName { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string IssueType { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = null!;

        public DateTime ReportedAt { get; set; } = Helpers.TimeHelper.VnNow;

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "pending";

        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedByUserId { get; set; }

        [MaxLength(1000)]
        public string? ResolutionNote { get; set; }

        public bool LaneReactivated { get; set; }

        [ForeignKey("LaneId")]
        public Lane Lane { get; set; } = null!;

        public ICollection<StaffLaneDispatch> Dispatches { get; set; } = new List<StaffLaneDispatch>();
    }
}
