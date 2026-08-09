using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services.Operations
{
    public interface IOperationsMonitoringService
    {
        Task<QueueMonitoringDashboardDTO> GetQueueMonitoringAsync(int branchId, CancellationToken cancellationToken = default);
        Task<List<BarrierCommandDTO>> GetFailedOrExpiredBarrierCommandsAsync(int branchId, CancellationToken cancellationToken = default);
        Task<List<ReconciliationAlertDTO>> RunReconciliationCheckAsync(int branchId, CancellationToken cancellationToken = default);
        Task<bool> AckBarrierCommandAsync(string commandId, string? status);
        Task<bool> IsEmployeeInBranchAsync(int userId, int branchId);
    }
    public class QueueMonitoringDashboardDTO
    {
        public List<LaneOccupancyDTO> OccupiedLanes { get; set; } = new();
        public List<WaitingVehicleDTO> WaitingQueue { get; set; } = new();
    }
    public class LaneOccupancyDTO
    {
        public int LaneId { get; set; }
        public string LaneName { get; set; } = null!;
        public string LicensePlate { get; set; } = null!;
        public int? BookingId { get; set; }
        public DateTime OccupiedAt { get; set; }
    }
    public class WaitingVehicleDTO
    {
        public int? BookingId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime ScheduledTime { get; set; }
    }
    public class BarrierCommandDTO
    {
        public string CommandId { get; set; } = null!;
        public string BarrierId { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string LicensePlate { get; set; } = null!;
        public int? LaneId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = null!;
    }
    public class ReconciliationAlertDTO
    {
        public string AlertType { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int? BookingId { get; set; }
        public string? LicensePlate { get; set; }
        public int? LaneId { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}
