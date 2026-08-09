using AutoWashPro.BLL.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface ICarModelService
    {
        Task<List<CarModelDTO>> GetActiveCarModelsAsync();
        Task<bool> CreateCarModelAsync(CreateCarModelDTO request);
        Task<bool> UpdateCarModelAsync(int id, UpdateCarModelDTO request);
        Task<bool> DeleteCarModelAsync(int id);
        Task<int> RequestNewCarModelAsync(int userId, RequestCarModelDTO request);
        Task<List<CarModelDTO>> GetPendingCarModelsAsync();
        Task<bool> ApproveCarModelAsync(int id, ApproveCarModelDTO request);
        Task<bool> RejectCarModelAsync(int id);
    }
}
