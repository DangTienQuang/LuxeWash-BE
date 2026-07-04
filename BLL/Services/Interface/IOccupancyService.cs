using System;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IOccupancyService
    {
        Task<double> GetBranchOccupancyRateAsync(int branchId, DateTime targetDate);
    }
}
