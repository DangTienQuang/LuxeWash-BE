using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.BLL.DTOs;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Http;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.Tests
{
    public class VehicleServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly Mock<IPhotoService> _mockPhotoService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly VehicleService _vehicleService;

        public VehicleServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AutoWashDbContext(options);
            _mockPhotoService = new Mock<IPhotoService>(MockBehavior.Default);
            _mockEmailService = new Mock<IEmailService>(MockBehavior.Default);

            _vehicleService = new VehicleService(_context, _mockPhotoService.Object, _mockEmailService.Object);
        }

        private async Task SeedBaseDataAsync(int userId, int vehicleTypeId, int carModelId)
        {
            _context.Users.Add(new User { UserId = userId, Email = "customer@test.com", PasswordHash = "hash", Role = "Customer", Status = "Active", PhoneNumber = "0123456789" });
            _context.VehicleTypes.Add(new VehicleType { Id = vehicleTypeId, Name = "SUV", BaseWeight = 1 });
            _context.CarModels.Add(new CarModel { Id = carModelId, VehicleTypeId = vehicleTypeId, Name = "CR-V", Brand = "Honda", IsActive = true, Status = "Approved" });

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task AddVehicleAsync_ValidPayload_ReturnsTrue_TC1()
        {
            // Arrange
            int userId = 10, vehicleTypeId = 10, carModelId = 10;
            await SeedBaseDataAsync(userId, vehicleTypeId, carModelId);

            var request = new CreateVehicleDTO
            {
                LicensePlate = "51F-123.45",
                VehicleTypeId = vehicleTypeId,
                CarModelId = carModelId,
                RegistrationPhotoUrl = "http://fake.url"
            };

            // Act
            var result = await _vehicleService.AddVehicleAsync(userId, request);

            // Assert
            Assert.True(result);
            var vehicleInDb = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "51F12345");
            Assert.NotNull(vehicleInDb);
            Assert.Equal(userId, vehicleInDb.UserId);
            Assert.Equal(vehicleTypeId, vehicleInDb.VehicleTypeId);
            Assert.False(vehicleInDb.IsDeleted);
        }

        [Fact]
        public async Task AddVehicleAsync_DuplicateLicensePlate_ThrowsException_TC2()
        {
            // Arrange
            int userId = 20, vehicleTypeId = 20, carModelId = 20;
            await SeedBaseDataAsync(userId, vehicleTypeId, carModelId);

            _context.Vehicles.Add(new Vehicle
            {
                LicensePlate = "51F12345", // Normalized
                UserId = userId,
                VehicleTypeId = vehicleTypeId,
                IsDeleted = false
            });
            await _context.SaveChangesAsync();

            var request = new CreateVehicleDTO
            {
                LicensePlate = "51F-123.45", // Formatted
                VehicleTypeId = vehicleTypeId,
                CarModelId = carModelId
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => _vehicleService.AddVehicleAsync(userId, request));
            Assert.Contains("already exists", exception.Message);
        }

        [Fact]
        public async Task GetMyVehiclesAsync_ReturnsUserVehicles_TC3()
        {
            // Arrange
            int userId = 30, vehicleTypeId = 30, carModelId = 30;
            await SeedBaseDataAsync(userId, vehicleTypeId, carModelId);

            _context.Vehicles.Add(new Vehicle { LicensePlate = "51A11111", UserId = userId, VehicleTypeId = vehicleTypeId, CarModelId = carModelId, IsDeleted = false });
            _context.Vehicles.Add(new Vehicle { LicensePlate = "51A22222", UserId = userId, VehicleTypeId = vehicleTypeId, CarModelId = carModelId, IsDeleted = false });
            _context.Vehicles.Add(new Vehicle { LicensePlate = "51A33333", UserId = userId, VehicleTypeId = vehicleTypeId, CarModelId = carModelId, IsDeleted = true });
            await _context.SaveChangesAsync();

            // Act
            var result = await _vehicleService.GetMyVehiclesAsync(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, v => v.LicensePlate == "51A11111");
            Assert.DoesNotContain(result, v => v.LicensePlate == "51A33333"); // Deleted should not be returned
        }

        [Fact]
        public async Task UpdateVehicleAsync_ValidPayload_UpdatesSuccessfully_TC4()
        {
            // Arrange
            int userId = 40, vehicleTypeId = 40, carModelId = 40;
            await SeedBaseDataAsync(userId, vehicleTypeId, carModelId);

            _context.Vehicles.Add(new Vehicle { LicensePlate = "51A11111", UserId = userId, VehicleTypeId = vehicleTypeId, CarModelId = carModelId, IsDeleted = false });
            await _context.SaveChangesAsync();

            var request = new UpdateVehicleDTO
            {
                VehicleTypeId = vehicleTypeId,
                CarModelId = carModelId,
                UserNote = "Updated Note"
            };

            // Act
            var result = await _vehicleService.UpdateVehicleAsync(userId, "51A11111", request);

            // Assert
            Assert.True(result);
            var updatedVehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "51A11111");
            Assert.NotNull(updatedVehicle);
            Assert.Equal("Updated Note", updatedVehicle.UserNote);
        }

        [Fact]
        public async Task DeleteVehicleAsync_ValidVehicle_SoftDeletes_TC5()
        {
            // Arrange
            int userId = 50, vehicleTypeId = 50, carModelId = 50;
            await SeedBaseDataAsync(userId, vehicleTypeId, carModelId);

            _context.Vehicles.Add(new Vehicle { LicensePlate = "51A11111", UserId = userId, VehicleTypeId = vehicleTypeId, CarModelId = carModelId, IsDeleted = false });
            await _context.SaveChangesAsync();

            // Act
            var result = await _vehicleService.DeleteVehicleAsync(userId, "51A11111");

            // Assert
            Assert.True(result);
            var deletedVehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "51A11111");
            Assert.NotNull(deletedVehicle);
            Assert.True(deletedVehicle.IsDeleted);
        }
    }
}
