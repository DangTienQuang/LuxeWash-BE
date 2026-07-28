namespace AutoWashPro.BLL.DTOs.Operations
{
    public class LaneDisplayLatestResponseDTO
    {
        public int BranchId { get; set; }
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
        public LaneDisplayEventDTO? LatestEvent { get; set; }
        public List<LaneDisplayLatestStateDTO> Lanes { get; set; } = new();
    }
}
