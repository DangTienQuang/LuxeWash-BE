namespace AutoWashPro.BLL.DTOs.Operations
{
    public class LaneDisplayLatestStateDTO
    {
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public LaneDisplayEventDTO? LatestEvent { get; set; }
    }
}
