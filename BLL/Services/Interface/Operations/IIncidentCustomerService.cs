using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IIncidentCustomerService
    {
        Task HandleCustomerDecisionAsync(int userId, long affectedBookingId, string decision, int? targetBranchId, int? targetSlotId);
    }
}
