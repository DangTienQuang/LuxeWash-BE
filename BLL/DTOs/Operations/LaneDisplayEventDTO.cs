namespace AutoWashPro.BLL.DTOs.Operations
{
    public class LaneDisplayEventDTO
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public int BranchId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = null!;
        public int? BookingId { get; set; }
        public string? LicensePlate { get; set; }
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public DateTime? DisplayUntil { get; set; }
    }
}
