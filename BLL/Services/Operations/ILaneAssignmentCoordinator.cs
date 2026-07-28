using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Operations
{
    public interface ILaneAssignmentCoordinator
    {
        Task AssignLaneForBookingAsync(int bookingId, int laneId);
        Task PublishWaitingAsync(int branchId, int bookingId, string? licensePlate);
        Task PublishAssignedAsync(int branchId, int bookingId, string? licensePlate, int laneId, string laneName);
        Task PublishProcessingAsync(int branchId, int bookingId, string? licensePlate, int laneId, string laneName);
        Task PublishClearedAsync(int branchId, int laneId, string laneName);
    }
}
