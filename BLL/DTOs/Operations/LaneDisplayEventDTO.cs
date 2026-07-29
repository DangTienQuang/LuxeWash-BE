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
        public int? LaneId { get; set; }
        public string? LaneName { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? ReasonCode { get; set; }
        public DateTime? DisplayUntil { get; set; }
        public string? BarrierCommandId { get; set; }
        public string? BarrierStatus { get; set; }
    }
}
