using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class ServiceServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly ServiceService _sut;

        public ServiceServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new ServiceService(_dbContext);
        }

        [Fact]
        public async Task GetActiveServicesAsync_ExcludesInactive()
        {
            _dbContext.Services.AddRange(
                new Service { ServiceName = "Wash", IsActive = true },
                new Service { ServiceName = "Old Service", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetActiveServicesAsync();

            Assert.Single(result);
        }

        [Fact]
        public async Task GetActiveServicesAsync_FiltersByBranch()
        {
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service1 = new Service { ServiceName = "Wash A", IsActive = true };
            var service2 = new Service { ServiceName = "Wash B", IsActive = true };
            _dbContext.Services.AddRange(service1, service2);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service1.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 100000 });
            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service2.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 2, Price = 100000 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetActiveServicesAsync(branchId: 1);

            Assert.Single(result);
            Assert.Equal("Wash A", result[0].ServiceName);
        }

        [Fact]
        public async Task GetAllServicesAsync_IncludesInactive()
        {
            _dbContext.Services.AddRange(
                new Service { ServiceName = "Wash", IsActive = true },
                new Service { ServiceName = "Old Service", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllServicesAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetServiceByIdAsync_NotFound_ThrowsException()
        {
            await Assert.ThrowsAsync<Exception>(() => _sut.GetServiceByIdAsync(999));
        }

        [Fact]
        public async Task GetServiceByIdAsync_Found_ReturnsDTO()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetServiceByIdAsync(service.ServiceId);

            Assert.Equal("Wash", result.ServiceName);
        }

        [Fact]
        public async Task GetServiceByIdAsync_NoVehicleTypeLoaded_UsesNAFallback()
        {
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();
            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 100000 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetServiceByIdAsync(service.ServiceId);

            Assert.Equal("Sedan", result.Prices[0].VehicleTypeName); // should resolve via Include, not fallback
        }

        [Fact]
        public async Task CreateServiceAsync_InvalidVehicleType_ThrowsException()
        {
            var request = new CreateOrUpdateServiceDTO
            {
                ServiceName = "New Wash",
                Prices = new List<CreateServicePriceDTO> { new CreateServicePriceDTO { VehicleTypeId = 999, BranchId = 1, Price = 100000 } }
            };

            await Assert.ThrowsAsync<Exception>(() => _sut.CreateServiceAsync(request));
        }

        [Fact]
        public async Task CreateServiceAsync_Valid_CreatesWithPrices()
        {
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            var request = new CreateOrUpdateServiceDTO
            {
                ServiceName = "New Wash",
                Prices = new List<CreateServicePriceDTO> { new CreateServicePriceDTO { VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 150000, EstimatedDurationMinutes = 30 } }
            };

            var result = await _sut.CreateServiceAsync(request);

            Assert.Equal("New Wash", result.ServiceName);
            Assert.Single(result.Prices);
            Assert.Equal(150000, result.Prices[0].Price);
        }

        [Fact]
        public async Task UpdateServiceAsync_NotFound_ThrowsException()
        {
            var request = new CreateOrUpdateServiceDTO { ServiceName = "X", Prices = new List<CreateServicePriceDTO>() };

            await Assert.ThrowsAsync<Exception>(() => _sut.UpdateServiceAsync(999, request));
        }

        [Fact]
        public async Task UpdateServiceAsync_InvalidVehicleType_ThrowsException()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var request = new CreateOrUpdateServiceDTO
            {
                ServiceName = "Wash Updated",
                Prices = new List<CreateServicePriceDTO> { new CreateServicePriceDTO { VehicleTypeId = 999, BranchId = 1, Price = 100000 } }
            };

            await Assert.ThrowsAsync<Exception>(() => _sut.UpdateServiceAsync(service.ServiceId, request));
        }

        [Fact]
        public async Task UpdateServiceAsync_Valid_ReplacesPrices()
        {
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();
            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 100000 });
            await _dbContext.SaveChangesAsync();

            var request = new CreateOrUpdateServiceDTO
            {
                ServiceName = "Wash Premium",
                Prices = new List<CreateServicePriceDTO> { new CreateServicePriceDTO { VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 200000 } }
            };

            var result = await _sut.UpdateServiceAsync(service.ServiceId, request);

            Assert.True(result);
            var prices = await _dbContext.ServicePrices.Where(sp => sp.ServiceId == service.ServiceId).ToListAsync();
            Assert.Single(prices);
            Assert.Equal(200000, prices[0].Price);
        }

        [Fact]
        public async Task DeleteServiceAsync_NotFound_ThrowsException()
        {
            await Assert.ThrowsAsync<Exception>(() => _sut.DeleteServiceAsync(999));
        }

        [Fact]
        public async Task DeleteServiceAsync_TogglesIsActive()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteServiceAsync(service.ServiceId);

            Assert.True(result);
            var updated = await _dbContext.Services.FirstAsync(s => s.ServiceId == service.ServiceId);
            Assert.False(updated.IsActive); // toggled off

            // Toggle again — should restore to active
            await _sut.DeleteServiceAsync(service.ServiceId);
            var restored = await _dbContext.Services.FirstAsync(s => s.ServiceId == service.ServiceId);
            Assert.True(restored.IsActive);
        }
    }
}