using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IServiceMaterialUsageService
    {
        Task<List<ServiceMaterialUsageDTO>> GetByServiceAsync(int serviceId);
        Task<List<ServiceMaterialUsageDTO>> UpsertAsync(int serviceId, UpsertServiceMaterialUsageDTO dto);
        Task<ServiceMaterialUsageDTO> UpdateAsync(int serviceId, int usageId, UpsertServiceMaterialUsageDTO dto);
        Task<List<VehicleConditionMaterialMultiplierDTO>> GetConditionMultipliersAsync();
        Task<VehicleConditionMaterialMultiplierDTO> UpdateConditionMultiplierAsync(int id, UpdateVehicleConditionMaterialMultiplierDTO dto);
    }
}
