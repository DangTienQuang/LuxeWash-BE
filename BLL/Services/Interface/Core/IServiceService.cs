using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
namespace AutoWashPro.BLL.Services
{
    public interface IServiceService
    {
        Task<List<ServiceDTO>> GetActiveServicesAsync(int? branchId = null);
        Task<List<ServiceDTO>> GetAllServicesAsync(int? branchId = null);
        Task<ServiceDTO> GetServiceByIdAsync(int id);
        Task<ServiceDTO> CreateServiceAsync(CreateOrUpdateServiceDTO request);
        Task<bool> UpdateServiceAsync(int id, CreateOrUpdateServiceDTO request);
        Task<bool> DeleteServiceAsync(int id); 
    }
}