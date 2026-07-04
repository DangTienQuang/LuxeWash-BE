using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IInventoryTransferService
    {
        Task<List<WarehouseStockDTO>> GetManagerStocksAsync(int managerUserId);
        Task<List<MaterialBatchDTO>> GetManagerBatchesAsync(int managerUserId, int? materialId = null, bool includeDepleted = false);
        Task<List<MaterialBatchDTO>> GetManagerExpiringBatchesAsync(int managerUserId);
        Task<MaterialBatchDTO> ImportBatchToManagerBranchAsync(int managerUserId, ImportMaterialBatchDTO dto);
        Task DiscardManagerBatchAsync(int managerUserId, int batchId, string? reason = null);
        Task<WarehouseStockDTO> AdjustManagerStockAsync(int managerUserId, AdjustBranchInventoryDTO dto);
        Task<List<InventoryTransactionDTO>> GetManagerTransactionsAsync(int managerUserId, int? materialId = null, DateTime? from = null, DateTime? to = null, string? type = null);
        Task<BranchInventorySettingDTO> GetBranchInventorySettingAsync(int branchId);
        Task<BranchInventorySettingDTO> UpdateBranchInventorySettingAsync(int branchId, UpdateBranchInventorySettingDTO dto);
    }
}
