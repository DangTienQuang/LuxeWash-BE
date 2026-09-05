using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class StaffLaneDispatch
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LaneId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        public int DispatchedByUserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string DispatchedByFullName { get; set; } = null!;

        [Required]
        public int StaffUserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string StaffFullName { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string Reason { get; set; } = "incident";

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime DispatchedAt { get; set; } = Helpers.TimeHelper.VnNow;

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "pending";

        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [MaxLength(500)]
        public string? NoteFromStaff { get; set; }

        [MaxLength(30)]
        public string? IncidentResolution { get; set; }

        public int? RelatedIncidentId { get; set; }

        [ForeignKey("LaneId")]
        public Lane Lane { get; set; } = null!;

        [ForeignKey("RelatedIncidentId")]
        public LaneIncident? RelatedIncident { get; set; }
    }
}
