using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class IncidentChange
    {
        [Key]
        public long Id { get; set; }

        public long IncidentId { get; set; }

        [ForeignKey("IncidentId")]
        public virtual BranchIncident Incident { get; set; } = null!;

        public int ActorUserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = null!; // "Created", "Extended", "Resolved", "OverrideAffectedBooking"

        public DateTime? OldEstimatedEndAtVn { get; set; }
        public DateTime? NewEstimatedEndAtVn { get; set; }

        [Required]
        public DateTime OccurredAtVn { get; set; } = DAL.Helpers.TimeHelper.VnNow;

        [MaxLength(500)]
        public string? Note { get; set; }

        [Column(TypeName = "json")]
        public string? SnapshotJson { get; set; }
    }
}
