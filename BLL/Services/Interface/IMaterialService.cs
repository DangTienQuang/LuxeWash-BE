using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IMaterialService
    {
        Task<List<MaterialDTO>> GetMaterialsAsync(bool includeInactive = false);
        Task<MaterialDTO> CreateMaterialAsync(CreateMaterialDTO dto);
        Task<MaterialDTO> UpdateMaterialAsync(int materialId, UpdateMaterialDTO dto);
        Task<List<WarehouseStockDTO>> GetStocksAsync(int? branchId = null);
        Task<List<MaterialBatchDTO>> GetBatchesAsync(int? branchId = null, bool expiringOnly = false);
        Task DiscardBatchAsync(int batchId, string? note = null);
    }
}
