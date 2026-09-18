using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class BranchIncident
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int BranchId { get; set; }

        [ForeignKey("BranchId")]
        public virtual Branch Branch { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = null!; // "LaneFailure", "PowerOutage", "WaterOutage", "Other"

        [Required]
        [MaxLength(50)]
        public string Scope { get; set; } = null!; // "SelectedLanes", "WholeBranch"

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = null!;

        [Required]
        public DateTime StartedAtVn { get; set; }

        [Required]
        public DateTime EstimatedEndAtVn { get; set; }

        public DateTime? ActualEndAtVn { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // "Active", "Resolved"

        public int CreatedByUserId { get; set; }
        public int? ResolvedByUserId { get; set; }

        [Required]
        public DateTime CreatedAtVn { get; set; } = DAL.Helpers.TimeHelper.VnNow;

        [Required]
        public DateTime UpdatedAtVn { get; set; } = DAL.Helpers.TimeHelper.VnNow;

        [ConcurrencyCheck]
        public int Version { get; set; } = 1;

        public virtual ICollection<IncidentLane> IncidentLanes { get; set; } = new List<IncidentLane>();
        public virtual ICollection<IncidentChange> IncidentChanges { get; set; } = new List<IncidentChange>();
        public virtual ICollection<IncidentAffectedBooking> AffectedBookings { get; set; } = new List<IncidentAffectedBooking>();
    }
}
