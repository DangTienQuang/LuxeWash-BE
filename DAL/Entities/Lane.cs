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

        public ICollection<StaffLaneAssignment> StaffAssignments { get; set; } = new List<StaffLaneAssignment>();
        public ICollection<Booking> ProcessingBookings { get; set; } = new List<Booking>();
    }
}
