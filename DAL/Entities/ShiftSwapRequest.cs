using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ShiftSwapRequest
    {
        [Key]
        public int ShiftSwapRequestId { get; set; }

        [Required]
        public int FromAssignmentId { get; set; }

        [ForeignKey("FromAssignmentId")]
        public StaffShiftAssignment FromAssignment { get; set; } = null!;

        public int? ToAssignmentId { get; set; }

        [ForeignKey("ToAssignmentId")]
        public StaffShiftAssignment? ToAssignment { get; set; }

        public int? ToWorkShiftId { get; set; }

        [ForeignKey("ToWorkShiftId")]
        public WorkShift? ToWorkShift { get; set; }

        public DateTime? ToWorkDate { get; set; }

        [Required]
        public int RequestedByUserId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? ReviewNote { get; set; }

        public DateTime CreatedAt { get; set; } = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
    }
}
