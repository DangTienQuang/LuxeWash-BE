using System;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class TimeSlotAdminResponseDTO
    {
        public int SlotId { get; set; }
        public int BranchId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsVipOnly { get; set; }
    }

    public class CreateTimeSlotDTO
    {
        [Required]
        public int BranchId { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "MaxCapacity must be greater than 0.")]
        public int MaxCapacity { get; set; } = 3;

        public bool IsVipOnly { get; set; } = false;
    }

    public class UpdateTimeSlotDTO
    {
        [Required]
        public int BranchId { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "MaxCapacity must be greater than 0.")]
        public int MaxCapacity { get; set; }

        public bool IsVipOnly { get; set; }
    }
}
