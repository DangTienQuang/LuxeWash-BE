using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ExtraMaterialUsageRequest
    {
        [Key]
        public int RequestId { get; set; }

        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        public int StaffUserId { get; set; }

        [ForeignKey(nameof(StaffUserId))]
        public User StaffUser { get; set; } = null!;

        public int BranchId { get; set; }

        [ForeignKey(nameof(BranchId))]
        public Branch Branch { get; set; } = null!;

        public int MaterialId { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public Material Material { get; set; } = null!;

        public decimal Quantity { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public int? ReviewedByManagerId { get; set; }

        [ForeignKey(nameof(ReviewedByManagerId))]
        public User? ReviewedByManager { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? ManagerNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
