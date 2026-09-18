using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class IncidentAffectedBooking
    {
        [Key]
        public long Id { get; set; }

        public long IncidentId { get; set; }

        [ForeignKey("IncidentId")]
        public virtual BranchIncident Incident { get; set; } = null!;

        public int BookingId { get; set; }
        
        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; } = null!;

        public int? ActiveBookingId { get; set; }

        public int? UserId { get; set; }
        
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "AwaitingCustomer"; // "AwaitingCustomer", "NeedsManualHandling", "Transferred", "Cancelled", "Kept"

        [Required]
        public DateTime ResponseDeadlineAtVn { get; set; }

        public DateTime? DecidedAtVn { get; set; }

        [MaxLength(50)]
        public string? Decision { get; set; } // "Cancel", "Transfer", "Keep"

        public int OriginalBranchId { get; set; }
        public DateTime OriginalScheduledTimeVn { get; set; }

        public int? TargetBranchId { get; set; }
        public int? TargetSlotId { get; set; }

        public int? CompensationUserVoucherId { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; } = 1;
    }
}
