using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class IncidentDelivery
    {
        [Key]
        public long Id { get; set; }

        public long AffectedBookingId { get; set; }

        [ForeignKey("AffectedBookingId")]
        public virtual IncidentAffectedBooking AffectedBooking { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Channel { get; set; } = null!; // "Push", "InApp", "Email"

        [Required]
        [MaxLength(50)]
        public string EventKind { get; set; } = null!; // "ActionRequired", "Resolution"

        [Required]
        [MaxLength(50)]
        public string State { get; set; } = "Pending"; // "Pending", "Sent", "Failed", "DeadLetter"

        public int AttemptCount { get; set; } = 0;
        public DateTime? LastAttemptAtVn { get; set; }

        public int IncidentVersion { get; set; }
    }
}
