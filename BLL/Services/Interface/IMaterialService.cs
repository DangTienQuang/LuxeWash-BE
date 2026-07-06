using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IMaterialService
    {
        Task<List<MaterialDTO>> GetMaterialsAsync(bool includeInactive = false);
        Task<List<MaterialUnitDTO>> GetMaterialUnitsAsync(bool includeInactive = false);
        Task<MaterialUnitDTO> CreateMaterialUnitAsync(CreateMaterialUnitDTO dto);
        Task<MaterialUnitDTO> UpdateMaterialUnitAsync(int unitId, UpdateMaterialUnitDTO dto);
        Task<MaterialDTO> CreateMaterialAsync(CreateMaterialDTO dto);
        Task<MaterialDTO> UpdateMaterialAsync(int materialId, UpdateMaterialDTO dto);
        Task<List<WarehouseStockDTO>> GetStocksAsync(int? branchId = null);
        Task<List<MaterialBatchDTO>> GetBatchesAsync(int? branchId = null, bool expiringOnly = false);
        Task DiscardBatchAsync(int batchId, string? note = null);
    }
}
