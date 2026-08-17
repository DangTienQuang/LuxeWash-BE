using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class OvertimeRequest
    {
        [Key]
        public int OvertimeRequestId { get; set; }

        [Required]
        public int StaffUserId { get; set; }

        [ForeignKey("StaffUserId")]
        public User StaffUser { get; set; } = null!;

        [Required]
        public DateTime WorkDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

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
