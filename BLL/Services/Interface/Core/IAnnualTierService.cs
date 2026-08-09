using System.Threading;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface IAnnualTierService
    {
        Task ResetAnnualTiersAsync(CancellationToken cancellationToken = default);
    }
}
