using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class StaffShiftAssignment
    {
        [Key]
        public int AssignmentId { get; set; }

        [Required]
        public int StaffUserId { get; set; }

        [ForeignKey("StaffUserId")]
        public User StaffUser { get; set; } = null!;

        [Required]
        public int WorkShiftId { get; set; }

        [ForeignKey("WorkShiftId")]
        public WorkShift WorkShift { get; set; } = null!;

        [Required]
        public DateTime WorkDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Scheduled";

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
