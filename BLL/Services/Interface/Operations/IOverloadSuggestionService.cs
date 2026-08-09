using AutoWashPro.BLL.DTOs;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services.Interface
{
    public interface IOverloadSuggestionService
    {
        Task<OverloadScanResultDTO> CheckAndTriggerOverloadAsync(int branchId);
    }
}
