using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class AssignBookingToLaneDTO
    {
        [Required]
        public int BookingId { get; set; }
        [Required]
        public int LaneId { get; set; }
        [Required]
        public int StaffId { get; set; }
    }
}
