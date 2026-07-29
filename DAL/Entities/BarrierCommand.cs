using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class BarrierCommand
    {
        [Key]
        [MaxLength(36)]
        public string CommandId { get; set; } = null!;

        [Required]
        public int BranchId { get; set; }

        [Required]
        [MaxLength(50)]
        public string BarrierId { get; set; } = null!; // ENTRY_GATE or EXIT_GATE

        [Required]
        [MaxLength(20)]
        public string Action { get; set; } = null!; // OPEN

        public int? BookingId { get; set; }
        
        public int? FleetWashLogId { get; set; }

        [MaxLength(20)]
        public string? LicensePlate { get; set; }

        public int? LaneId { get; set; }

        [MaxLength(36)]
        public string? AdmissionId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Published, Acknowledged, Failed, Expired
    }
}
