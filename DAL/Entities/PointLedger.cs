using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class PointLedger
    {
        [Key]
        public int LedgerId { get; set; }

        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        public int PointsAdded { get; set; } = 0;
        public int PointsDeducted { get; set; } = 0;

        public string Reason { get; set; } = null!;

        public DateTime TransactionDate { get; set; } = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;

        public DateTime? ExpiryDate { get; set; }

        public int? ReferenceBookingId { get; set; }
    }
}