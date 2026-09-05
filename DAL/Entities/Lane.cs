using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class Lane
    {
        [Key]
        public int LaneId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;

        [Required]
        public int BranchId { get; set; }

        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        public bool IsActive { get; set; } = true;
        public bool IsBusinessLane { get; set; }
        public bool IsVipLane { get; set; } = false;
        [MaxLength(255)]
        public string? DeactivationReason { get; set; }
        public int? DeactivatedByUserId { get; set; }
        public DateTime? DeactivatedAt { get; set; }

        public ICollection<Booking> ProcessingBookings { get; set; } = new List<Booking>();
        public ICollection<LaneIncident> LaneIncidents { get; set; } = new List<LaneIncident>();
        public ICollection<StaffLaneDispatch> StaffLaneDispatches { get; set; } = new List<StaffLaneDispatch>();
    }
}
