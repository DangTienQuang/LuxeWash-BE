using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
namespace AutoWashPro.BLL.Services
{
    public interface IVehicleTypeService
    {
        Task<List<VehicleTypeDTO>> GetAllAsync();
        Task<VehicleTypeDTO> CreateAsync(CreateVehicleTypeDTO request);
        Task<bool> UpdateAsync(int id, CreateVehicleTypeDTO request);
        Task<bool> DeleteAsync(int id);
    }
}