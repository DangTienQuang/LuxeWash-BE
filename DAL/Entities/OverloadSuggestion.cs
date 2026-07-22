using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class OverloadSuggestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey("BookingId")]
        public Booking Booking { get; set; } = null!;

        [Required]
        public int SuggestedBranchId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SuggestedBranchName { get; set; } = null!;

        [Required]
        public int SuggestedSlotId { get; set; }

        [Required]
        public DateTime SuggestedTime { get; set; }

        public bool IsProcessed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
    }
}
