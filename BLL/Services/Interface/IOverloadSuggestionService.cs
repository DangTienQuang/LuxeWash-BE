using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IOverloadSuggestionService
    {
        Task CheckAndTriggerOverloadAsync(int branchId);
    }
}
