namespace BLL.DTOs.Fleet
{
    public class FleetHistoryFilterDTO
    {
        public int? FleetVehicleId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class FleetHistoryDTO
    {
        public int FleetWashLogId { get; set; }
        public int FleetVehicleId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string? DriverName { get; set; }
        public string BranchName { get; set; } = null!;
        public DateTime CheckInTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public decimal WashCost { get; set; }
        public string Status { get; set; } = null!;
    }

    public class FleetDashboardDTO
    {
        public int TotalVehicles { get; set; }
        public int ActiveVehicles { get; set; }
        public int PendingVehicles { get; set; }
        public int TodayWashCount { get; set; }
        public int MonthlyWashCount { get; set; }
        public decimal MonthlySpend { get; set; }
        public int VehiclesCurrentlyInStation { get; set; }
    }

    public class FleetWashHistoryDTO
    {
        public int FleetWashLogId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string VehicleType { get; set; } = null!;
        public string BranchName { get; set; } = null!;
        public DateTime CheckInTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public string Status { get; set; } = null!;
        public decimal WashCost { get; set; }
        public int? BookingId { get; set; }
        public string WashType { get; set; } = null!;
    }
}