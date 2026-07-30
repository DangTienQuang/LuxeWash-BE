using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IVehicleService
    {
        Task<List<VehicleDTO>> GetMyVehiclesAsync(int userId);
        Task<bool> AddVehicleAsync(int userId, CreateVehicleDTO request);
        Task<bool> UpdateVehicleAsync(int userId, string licensePlate, UpdateVehicleDTO request);
        Task<bool> DeleteVehicleAsync(int userId, string licensePlate);
        Task<VehicleRecognitionDTO> RecognizeVehicleAsync(string licensePlate);
        Task<List<AdminOtherVehicleDTO>> GetOtherVehiclesAsync();
        Task<bool> UpdateVehicleTypeByAdminAsync(string licensePlate, int newVehicleTypeId);
        Task<bool> ApproveNewVehicleTypeAsync(string licensePlate, ApproveVehicleTypeRequestDTO request);
        Task<bool> RejectNewVehicleTypeAsync(string licensePlate);
        /// <summary>Returns the vehicle type name for a given plate from Customer or Fleet records. Returns null if not found.</summary>
        Task<string?> GetVehicleTypeNameByPlateAsync(string normalizedPlate);
    }
}