using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class CarModelService : ICarModelService
    {
        private readonly AutoWashDbContext _context;
        private readonly ILogger<CarModelService> _logger;

        public CarModelService(AutoWashDbContext context, ILogger<CarModelService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<CarModelDTO>> GetActiveCarModelsAsync()
        {
            return await _context.CarModels
                .Where(c => c.IsActive && c.Status == "Approved")
                .OrderBy(c => c.Brand).ThenBy(c => c.Name)
                .Select(c => new CarModelDTO
                {
                    Id = c.Id,
                    Brand = c.Brand,
                    Name = c.Name,
                    Status = c.Status,
                    RequestedByUserId = c.RequestedByUserId,
                    VehicleTypeId = c.VehicleTypeId
                })
                .ToListAsync();
        }

        public async Task<bool> CreateCarModelAsync(CreateCarModelDTO request)
        {
            if (request.VehicleTypeId.HasValue)
            {
                var vehicleTypeExists = await _context.VehicleTypes.AnyAsync(vt => vt.Id == request.VehicleTypeId.Value);
                if (!vehicleTypeExists) throw new BadRequestException("Invalid vehicle type.");
            }

            var newModel = new CarModel
            {
                Brand = request.Brand,
                Name = request.Name,
                Status = "Approved",
                VehicleTypeId = request.VehicleTypeId
            };
            _context.CarModels.Add(newModel);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<bool> UpdateCarModelAsync(int id, UpdateCarModelDTO request)
        {
            var model = await _context.CarModels.FindAsync(id);
            if (model == null) return false;

            model.Brand = request.Brand;
            model.Name = request.Name;
            model.IsActive = request.IsActive;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCarModelAsync(int id)
        {
            var model = await _context.CarModels.FindAsync(id);
            if (model == null) return false;

            model.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> RequestNewCarModelAsync(int userId, RequestCarModelDTO request)
        {
            int? finalVehicleTypeId = request.VehicleTypeId;

            if (finalVehicleTypeId.HasValue)
            {
                var vehicleTypeExists = await _context.VehicleTypes.AnyAsync(vt => vt.Id == finalVehicleTypeId.Value);
                if (!vehicleTypeExists) throw new BadRequestException("Invalid vehicle type.");
            }
            else
            {
                // Auto-assign to "Other" (Other) if FE doesn't provide one
                var otherVehicleType = await _context.VehicleTypes
                    .FirstOrDefaultAsync(vt => vt.Name.Contains("Other") || vt.Name.Contains("Other"));

                if (otherVehicleType != null)
                {
                    finalVehicleTypeId = otherVehicleType.Id;
                }
                else
                {
                    var newOtherType = new AutoWashPro.DAL.Entities.VehicleType
                    {
                        Name = "Other",
                        Description = "Unclassified vehicle types",
                        BaseWeight = 1
                    };
                    _context.VehicleTypes.Add(newOtherType);
                    await _context.SaveChangesAsync();
                    finalVehicleTypeId = newOtherType.Id;
                }
            }

            string combinedName = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Version))
            {
                combinedName += $" {request.Version.Trim()}";
            }
            if (request.Year.HasValue)
            {
                combinedName += $" {request.Year.Value}";
            }

            var newModel = new CarModel
            {
                Brand = request.Brand.Trim(),
                Name = combinedName,
                Status = "Pending",
                IsActive = true,
                RequestedByUserId = userId,
                VehicleTypeId = finalVehicleTypeId
            };

            _context.CarModels.Add(newModel);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"[Staff Notification] New vehicle model '{newModel.Brand} {newModel.Name}' requires verification (ID: {newModel.Id}).");

            return newModel.Id;
        }

        public async Task<List<CarModelDTO>> GetPendingCarModelsAsync()
        {
            return await _context.CarModels
                .Where(c => c.IsActive && c.Status == "Pending")
                .OrderByDescending(c => c.Id)
                .Select(c => new CarModelDTO
                {
                    Id = c.Id,
                    Brand = c.Brand,
                    Name = c.Name,
                    Status = c.Status,
                    RequestedByUserId = c.RequestedByUserId,
                    VehicleTypeId = c.VehicleTypeId
                })
                .ToListAsync();
        }

        public async Task<bool> ApproveCarModelAsync(int id, ApproveCarModelDTO request)
        {
            var model = await _context.CarModels.FindAsync(id);
            if (model == null) throw new NotFoundException("Car model not found.");
            if (model.Status != "Pending") throw new BadRequestException("Can only approve car models in pending status.");

            var vehicleTypeExists = await _context.VehicleTypes.AnyAsync(vt => vt.Id == request.VehicleTypeId);
            if (!vehicleTypeExists) throw new BadRequestException("Invalid vehicle type.");

            model.Status = "Approved";
            model.VehicleTypeId = request.VehicleTypeId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectCarModelAsync(int id)
        {
            var model = await _context.CarModels.FindAsync(id);
            if (model == null) throw new NotFoundException("Car model not found.");
            if (model.Status != "Pending") throw new BadRequestException("Can only reject car models in pending status.");

            model.Status = "Rejected";
            model.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
