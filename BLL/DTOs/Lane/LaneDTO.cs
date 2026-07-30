namespace AutoWashPro.BLL.DTOs
{
    public class LaneDTO
    {
        public int LaneId { get; set; }
        public string Name { get; set; } = null!;
        public int BranchId { get; set; }
        public bool IsActive { get; set; }
        public bool IsBusinessLane { get; set; }
        public bool IsVipLane { get; set; }
    }
}
