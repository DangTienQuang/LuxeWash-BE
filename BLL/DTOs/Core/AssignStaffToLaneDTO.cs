using System;
using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class AssignStaffToLaneDTO
    {
        [Required]
        public int StaffId { get; set; }
        [Required]
        public int LaneId { get; set; }
        [Required]
        public DateTime AssignedDate { get; set; }
        [Required]
        public int WorkShiftId { get; set; }
    }
}
