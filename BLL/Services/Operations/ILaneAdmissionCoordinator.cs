using AutoWashPro.BLL.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Operations
{
    public enum LaneReleaseMode
    {
        PhysicalCheckout,
        AdministrativeRelease
    }

    public interface ILaneAdmissionCoordinator
    {
        Task<GateCheckInResult> CheckInAtEntryGateAsync(
            string licensePlate,
            int branchId,
            int? bookingId = null,
            int? fleetWashLogId = null,
            int? forcedLaneId = null,
            CancellationToken cancellationToken = default);

        Task<CheckOutResult> CheckOutAtExitGateAsync(
            string licensePlate,
            int branchId,
            CancellationToken cancellationToken = default);

        Task<CheckOutResult> CompletePhysicalCheckoutAsync(
            int bookingId,
            int employeeId,
            CancellationToken cancellationToken = default);
            
        Task<CheckOutResult> ReleaseLaneAsync(
            int bookingId,
            string reason,
            CancellationToken cancellationToken = default);

        Task<AdmissionResult?> AdmitNextWaitingVehicleAsync(
            int laneId,
            CancellationToken cancellationToken = default);
    }
    
    // Supporting DTOs
    public class GateCheckInResult
    {
        public int? BookingId { get; set; }
        public int? FleetWashLogId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string AdmissionStatus { get; set; } = null!;
        public bool IsWaiting { get; set; }
        public int? LaneId { get; set; }
        public string? LaneName { get; set; }
        public string? BarrierCommandId { get; set; }
        public bool BarrierCommandCreated { get; set; }
        public string? BarrierId { get; set; }
        public string? Message { get; set; }
    }

    public class CheckOutResult
    {
        public int? CompletedBookingId { get; set; }
        public int? CompletedFleetWashLogId { get; set; }
        public int ReleasedLaneId { get; set; }
        public string? ExitBarrierCommandId { get; set; }
        public string? BarrierId { get; set; }
        public AdmissionResult? NextAdmission { get; set; }
    }

    public class AdmissionResult
    {
        public int? BookingId { get; set; }
        public int? FleetWashLogId { get; set; }
        public string LicensePlate { get; set; } = null!;
        public int LaneId { get; set; }
        public string? LaneName { get; set; }
        public string? EntryBarrierCommandId { get; set; }
        public string? BarrierId { get; set; }
    }
}
