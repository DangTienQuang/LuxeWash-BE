using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IIncidentCustomerService
    {
        Task HandleCustomerDecisionAsync(int userId, long affectedBookingId, string decision, int? targetBranchId, int? targetSlotId);
        Task SystemCancelAsync(long affectedBookingId);
        Task<IncidentAffectedBookingMobileDTO> GetAffectedBookingDetailsAsync(int userId, int bookingId);
    }
}
