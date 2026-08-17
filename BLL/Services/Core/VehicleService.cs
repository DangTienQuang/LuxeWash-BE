#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public class VehicleService : IVehicleService
    {
        private readonly AutoWashDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IPhotoService _photoService;
        private readonly AutoWashPro.BLL.Services.Interface.IUserNotificationService _userNotificationService;

        public VehicleService(AutoWashDbContext context, IPhotoService photoService, IEmailService emailService, AutoWashPro.BLL.Services.Interface.IUserNotificationService userNotificationService)
        {
            _context = context;
            _photoService = photoService;
            _emailService = emailService;
            _userNotificationService = userNotificationService;
        }

        public async Task<List<VehicleDTO>> GetMyVehiclesAsync(int userId)
        {
            return await _context.Vehicles
                .Include(v => v.VehicleType)
                .Include(v => v.CarModelEntity)
                .Where(v => v.UserId == userId && !v.IsDeleted)
                .Select(v => new VehicleDTO
                {
                    LicensePlate = v.LicensePlate,
                    VehicleTypeId = v.VehicleTypeId,
                    VehicleType = v.VehicleType.Name,
                    RegistrationPhotoUrl = v.RegistrationPhotoUrl,
                    CarModel = v.CarModelId.HasValue ? v.CarModelEntity.Name : v.CarModel,
                    Brand = v.CarModelId.HasValue ? v.CarModelEntity.Brand : null,
                    UserNote = v.UserNote
                }).ToListAsync();
        }
        private string NormalizeLicensePlate(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return string.Empty;
            return new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
        }
        public async Task<VehicleDTO> AddVehicleAsync(int userId, CreateVehicleDTO request)
        {
            int? finalVehicleTypeId = request.VehicleTypeId > 0 ? request.VehicleTypeId : null;
            CarModel? carModel = null;
            if (request.CarModelId.HasValue)
            {
                carModel = await _context.CarModels.FirstOrDefaultAsync(c => c.Id == request.CarModelId.Value && c.IsActive && c.Status != "Rejected");
                if (carModel == null)
                    throw new BadRequestException("Selected car model does not exist or is no longer supported.");
                if (carModel.VehicleTypeId.HasValue)
                {
                    finalVehicleTypeId = carModel.VehicleTypeId.Value;
                }
            }
            if (!finalVehicleTypeId.HasValue) throw new BadRequestException("Please select a vehicle type.");
            var vehicleType = await _context.VehicleTypes.FirstOrDefaultAsync(t => t.Id == finalVehicleTypeId.Value);
            if (vehicleType == null) throw new BadRequestException("Invalid vehicle type.");
            string finalPhotoUrl = request.RegistrationPhotoUrl;
            if (request.PhotoFile != null && request.PhotoFile.Length > 0)
            {
                finalPhotoUrl = await _photoService.UploadImageAsync(request.PhotoFile);
            }
            if (vehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase) ||
                vehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(finalPhotoUrl))
                {
                    throw new BadRequestException("You must upload an actual photo of the vehicle when selecting 'Other' vehicle type.");
                }
                if (string.IsNullOrWhiteSpace(request.UserNote))
                {
                    throw new BadRequestException("Please leave a note of your car model name so we can support updating it.");
                }
            }
            var vehicleCount = await _context.Vehicles.CountAsync(v => v.UserId == userId && !v.IsDeleted);
            if (vehicleCount >= 5)
            {
                throw new BadRequestException("Personal profile can link a maximum of 5 vehicles. Please contact Customer Support if you have a larger fleet.");
            }
            var normalizedPlate = NormalizeLicensePlate(request.LicensePlate);
            int? finalCarModelId = null;
            string? finalCarModel = null;
            if (request.CarModelId.HasValue)
            {
                var carModelExists = await _context.CarModels.AnyAsync(c => c.Id == request.CarModelId.Value && c.IsActive);
                if (!carModelExists)
                    throw new BadRequestException("Selected car model does not exist or is no longer supported.");
                finalCarModelId = request.CarModelId.Value;
                finalCarModel = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.CarModel))
                    throw new BadRequestException("Please enter your car model name when selecting 'Other'.");
                finalCarModelId = null;
                finalCarModel = request.CarModel.Trim();
            }
            var existingVehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == normalizedPlate);
            if (existingVehicle != null)
            {
                if (!existingVehicle.IsDeleted)
                {
                    if (existingVehicle.UserId == userId)
                    {
                        throw new BadRequestException("Biển số xe này đã có trong danh sách xe của bạn. Vui lòng tải lại trang (F5) nếu bạn không thấy.");
                    }
                    throw new BadRequestException("This license plate already exists in the system.");
                }
                existingVehicle.IsDeleted = false;
                existingVehicle.UserId = userId;
                existingVehicle.VehicleTypeId = finalVehicleTypeId.Value;
                existingVehicle.RegistrationPhotoUrl = finalPhotoUrl;
                existingVehicle.UserNote = request.UserNote;
                existingVehicle.CarModelId = finalCarModelId;
                existingVehicle.CarModel = finalCarModel;
            }
            else
            {
                var vehicle = new Vehicle
                {
                    LicensePlate = normalizedPlate,
                    VehicleTypeId = finalVehicleTypeId.Value,
                    UserId = userId,
                    RegistrationPhotoUrl = finalPhotoUrl,
                    UserNote = request.UserNote,
                    CarModelId = finalCarModelId,
                    CarModel = finalCarModel
                };
                _context.Vehicles.Add(vehicle);
            }
            await _context.SaveChangesAsync();

            await _userNotificationService.CreateNotificationAsync(
                userId,
                "Thêm xe thành công",
                $"Biển số xe {normalizedPlate} đã được thêm vào tài khoản của bạn.",
                "Vehicle",
                normalizedPlate
            );

            return new VehicleDTO
            {
                LicensePlate = normalizedPlate,
                VehicleTypeId = finalVehicleTypeId.Value,
                VehicleType = vehicleType.Name,
                RegistrationPhotoUrl = finalPhotoUrl,
                CarModel = finalCarModelId.HasValue ? carModel?.Name : finalCarModel,
                Brand = finalCarModelId.HasValue ? carModel?.Brand : null,
                UserNote = request.UserNote
            };
        }
        public async Task<List<AdminOtherVehicleDTO>> GetOtherVehiclesAsync()
        {
            return await _context.Vehicles
                .Include(v => v.VehicleType)
                .Include(v => v.User)
                    .ThenInclude(u => u.CustomerProfile)
                .Where(v => (!v.IsDeleted) && (v.VehicleType.Name.Contains("Other") || v.VehicleType.Name.Contains("Other")))
                .Select(v => new AdminOtherVehicleDTO
                {
                    LicensePlate = v.LicensePlate,
                    VehicleTypeId = v.VehicleTypeId,
                    VehicleTypeName = v.VehicleType.Name,
                    UserId = v.UserId,
                    OwnerName = v.User.CustomerProfile != null ? v.User.CustomerProfile.FullName : null,
                    OwnerPhone = v.User != null ? v.User.PhoneNumber : null,
                    RegistrationPhotoUrl = v.RegistrationPhotoUrl,
                    UserNote = v.UserNote,
                    CarModel = v.CarModel
                }).ToListAsync();
        }
        public async Task<bool> ApproveNewVehicleTypeAsync(string licensePlate, ApproveVehicleTypeRequestDTO request)
        {
            licensePlate = NormalizeLicensePlate(Uri.UnescapeDataString(licensePlate));
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var vehicle = await _context.Vehicles
                    .Include(v => v.VehicleType)
                    .Include(v => v.User)
                    .FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && !v.IsDeleted);
                if (vehicle == null) throw new NotFoundException("Vehicle not found.");
                if (!vehicle.VehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase) &&
                    !vehicle.VehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BadRequestException("This vehicle is not in the pending vehicle type approval list.");
                }
                var finalTypeName = string.IsNullOrWhiteSpace(request.CustomizedTypeName)
                    ? vehicle.UserNote
                    : request.CustomizedTypeName;
                if (string.IsNullOrWhiteSpace(finalTypeName))
                {
                     throw new BadRequestException("Vehicle type name cannot be empty. Please provide a vehicle type name.");
                }
                finalTypeName = finalTypeName.Trim();
                if (finalTypeName.Length > 50)
                {
                    throw new BadRequestException("Vehicle type name cannot exceed 50 characters.");
                }
                var description = string.IsNullOrWhiteSpace(request.Description)
                    ? "Approved from user request"
                    : request.Description.Trim();
                var existingType = await _context.VehicleTypes
                    .FirstOrDefaultAsync(t => t.Name.ToLower() == finalTypeName.ToLower());
                int finalTypeId;
                if (existingType != null)
                {
                    finalTypeId = existingType.Id;
                }
                else
                {
                    var newType = new VehicleType
                    {
                        Name = finalTypeName,
                        Description = description
                    };
                    _context.VehicleTypes.Add(newType);
                    await _context.SaveChangesAsync();
                    finalTypeId = newType.Id;
                }
                vehicle.VehicleTypeId = finalTypeId;
                vehicle.UserNote = null;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                if (vehicle.User != null && !string.IsNullOrWhiteSpace(vehicle.User.Email))
                {
                    var subject = "Request to add new vehicle type approved";
                    var message = $"Hello,<br/><br/>Your request to add a vehicle type for vehicle with license plate <b>{vehicle.LicensePlate}</b> has been successfully approved by the administrator. Your vehicle type is now <b>{finalTypeName}</b>.<br/><br/>Best regards,<br/>AutoWashPro Team.";
                    _ = Task.Run(() => _emailService.SendEmailAsync(vehicle.User.Email, subject, message));
                }
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> RejectNewVehicleTypeAsync(string licensePlate)
        {
            licensePlate = NormalizeLicensePlate(Uri.UnescapeDataString(licensePlate));
            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleType)
                .Include(v => v.User)
                .FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && !v.IsDeleted);
            if (vehicle == null) throw new NotFoundException("Vehicle not found.");
            if (!vehicle.VehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase) &&
                !vehicle.VehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("This vehicle is not in the pending vehicle type approval list.");
            }
            vehicle.IsDeleted = true;
            await _context.SaveChangesAsync();
            if (vehicle.User != null && !string.IsNullOrWhiteSpace(vehicle.User.Email))
            {
                var subject = "Request to add vehicle rejected";
                var message = $"Hello,<br/><br/>Your request to add vehicle with license plate <b>{vehicle.LicensePlate}</b> has been rejected due to invalid vehicle type information. Please re-register the vehicle with correct details.<br/><br/>Best regards,<br/>AutoWashPro Team.";
                _ = Task.Run(() => _emailService.SendEmailAsync(vehicle.User.Email, subject, message));
            }
            return true;
        }
        public async Task<bool> UpdateVehicleTypeByAdminAsync(string licensePlate, int newVehicleTypeId)
        {
            licensePlate = NormalizeLicensePlate(Uri.UnescapeDataString(licensePlate));
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && !v.IsDeleted);
            if (vehicle == null) throw new NotFoundException("Vehicle not found.");
            var typeExists = await _context.VehicleTypes.AnyAsync(t => t.Id == newVehicleTypeId);
            if (!typeExists) throw new BadRequestException("New vehicle type is invalid.");
            if (vehicle.VehicleTypeId != newVehicleTypeId)
            {
                var activeBooking = await _context.Bookings.AnyAsync(b => b.LicensePlate == licensePlate && (b.Status == "Pending" || b.Status == "CheckedIn"));
                if (activeBooking)
                    throw new BadRequestException("Cannot change vehicle type because there is an active booking for this vehicle. Please complete or cancel the booking first.");
            }
            vehicle.VehicleTypeId = newVehicleTypeId;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> UpdateVehicleAsync(int userId, string licensePlate, UpdateVehicleDTO request)
        {
            licensePlate = NormalizeLicensePlate(Uri.UnescapeDataString(licensePlate));
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && v.UserId == userId && !v.IsDeleted);
            if (vehicle == null) throw new NotFoundException("Vehicle not found or you do not have permission to modify this vehicle.");
            int? finalVehicleTypeId = request.VehicleTypeId > 0 ? request.VehicleTypeId : null;
            if (request.CarModelId.HasValue)
            {
                var carModel = await _context.CarModels.FirstOrDefaultAsync(c => c.Id == request.CarModelId.Value && c.IsActive && c.Status != "Rejected");
                if (carModel == null)
                    throw new BadRequestException("Selected car model does not exist or is no longer supported.");
                if (carModel.VehicleTypeId.HasValue)
                {
                    finalVehicleTypeId = carModel.VehicleTypeId.Value;
                }
            }
            if (!finalVehicleTypeId.HasValue) throw new BadRequestException("Please select a vehicle type.");
            var vehicleType = await _context.VehicleTypes.FirstOrDefaultAsync(t => t.Id == finalVehicleTypeId.Value);
            if (vehicleType == null) throw new BadRequestException("Invalid vehicle type.");
            string finalPhotoUrl = vehicle.RegistrationPhotoUrl;
            if (request.PhotoFile != null && request.PhotoFile.Length > 0)
            {
                finalPhotoUrl = await _photoService.UploadImageAsync(request.PhotoFile);
            }
            if (vehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase) ||
                vehicleType.Name.Contains("Other", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(finalPhotoUrl))
                {
                    throw new BadRequestException("You must upload an actual photo of the vehicle when selecting 'Other' vehicle type.");
                }
                if (string.IsNullOrWhiteSpace(request.UserNote) && string.IsNullOrWhiteSpace(vehicle.UserNote))
                {
                    throw new BadRequestException("Please leave a note of your car model name so we can support updating it.");
                }
            }
            int? finalCarModelId = null;
            string? finalCarModel = null;
            if (request.CarModelId.HasValue)
            {
                var carModelExists = await _context.CarModels.AnyAsync(c => c.Id == request.CarModelId.Value && c.IsActive);
                if (!carModelExists)
                    throw new BadRequestException("Selected car model does not exist or is no longer supported.");
                finalCarModelId = request.CarModelId.Value;
                finalCarModel = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.CarModel))
                    throw new BadRequestException("Please enter your car model name when selecting 'Other'.");
                finalCarModelId = null;
                finalCarModel = request.CarModel.Trim();
            }
            if (vehicle.VehicleTypeId != finalVehicleTypeId.Value)
            {
                var activeBooking = await _context.Bookings.AnyAsync(b => b.LicensePlate == licensePlate && (b.Status == "Pending" || b.Status == "CheckedIn"));
                if (activeBooking)
                    throw new BadRequestException("Cannot change vehicle type because there is an active booking for this vehicle. Please complete or cancel the booking first.");
            }
            vehicle.VehicleTypeId = finalVehicleTypeId.Value;
            vehicle.RegistrationPhotoUrl = finalPhotoUrl;
            if (!string.IsNullOrWhiteSpace(request.UserNote))
            {
                vehicle.UserNote = request.UserNote;
            }
            vehicle.CarModelId = finalCarModelId;
            vehicle.CarModel = finalCarModel;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> DeleteVehicleAsync(int userId, string licensePlate)
        {
            licensePlate = NormalizeLicensePlate(Uri.UnescapeDataString(licensePlate));
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && v.UserId == userId && !v.IsDeleted);
            if (vehicle == null) throw new NotFoundException("Vehicle not found or you do not have permission to delete this vehicle.");
            vehicle.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<VehicleRecognitionDTO> RecognizeVehicleAsync(string licensePlate)
        {
            licensePlate = NormalizeLicensePlate(Uri.UnescapeDataString(licensePlate));
            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleType)
                .Include(v => v.User)
                    .ThenInclude(u => u.CustomerProfile)
                        .ThenInclude(cp => cp.Tier)
                .FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && !v.IsDeleted);
            if (vehicle == null)
                throw new NotFoundException("License plate is not registered in the system.");
            if (vehicle.User == null || vehicle.User.CustomerProfile == null)
                throw new BadRequestException("Data error: Vehicle has no owner information.");
            var today = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.Date;
            var activeBooking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .Where(b => b.LicensePlate == licensePlate
                         && (b.Status == "Pending" || b.Status == "CheckedIn")
                         && b.ScheduledTime.Date == today)
                .OrderBy(b => b.ScheduledTime)
                .FirstOrDefaultAsync();
            return new VehicleRecognitionDTO
            {
                LicensePlate = vehicle.LicensePlate,
                VehicleType = vehicle.VehicleType?.Name ?? "N/A",
                OwnerName = vehicle.User.CustomerProfile.FullName,
                OwnerPhone = vehicle.User.PhoneNumber,
                TierName = vehicle.User.CustomerProfile.Tier?.TierName ?? "N/A",
                HasActiveBooking = activeBooking != null,
                ActiveBookingId = activeBooking?.BookingId,
                ScheduledTime = activeBooking?.ScheduledTime,
                CarModel = vehicle.CarModel
            };
        }
        public async Task<string?> GetVehicleTypeNameByPlateAsync(string normalizedPlate)
        {
            var customerVehicle = await _context.Vehicles
                .Include(v => v.VehicleType)
                .Where(v => v.LicensePlate == normalizedPlate && !v.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (customerVehicle != null)
                return customerVehicle.VehicleType?.Name;
            var fleetVehicle = await _context.FleetVehicles
                .Include(fv => fv.VehicleType)
                .Where(fv => fv.LicensePlate == normalizedPlate && fv.Status == "Active")
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return fleetVehicle?.VehicleType?.Name;
        }
    }
}
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
