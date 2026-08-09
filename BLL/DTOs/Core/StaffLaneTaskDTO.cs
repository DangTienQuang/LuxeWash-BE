using System;
namespace AutoWashPro.BLL.DTOs
{
    public class StaffLaneTaskDTO
    {
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public DateTime AssignedDate { get; set; }
    }
}
