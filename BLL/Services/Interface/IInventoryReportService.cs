using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IInventoryReportService
    {
        Task<InventoryReportDTO> GetAdminProfitReportAsync(DateTime? from, DateTime? to, int? branchId);
        Task<InventoryReportDTO> GetManagerProfitReportAsync(int managerUserId, DateTime? from, DateTime? to);
    }
}
