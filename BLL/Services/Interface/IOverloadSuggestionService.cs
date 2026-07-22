using AutoWashPro.BLL.DTOs;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IOverloadSuggestionService
    {
        /// <summary>
        /// Checks if a branch is overloaded and creates OverloadSuggestion rows for affected bookings.
        /// Returns a structured result containing counts of created suggestions and notifications sent.
        /// </summary>
        Task<OverloadScanResultDTO> CheckAndTriggerOverloadAsync(int branchId);
    }
}
