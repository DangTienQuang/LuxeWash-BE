using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class CarModelServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<ILogger<CarModelService>> _loggerMock;
        private readonly CarModelService _sut;

        public CarModelServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _loggerMock = new Mock<ILogger<CarModelService>>();
            _sut = new CarModelService(_dbContext, _loggerMock.Object);
        }

        [Fact]
        public async Task GetActiveCarModelsAsync_ReturnsOnlyActiveApproved()
        {
            _dbContext.CarModels.AddRange(
                new CarModel { Brand = "Toyota", Name = "Camry", Status = "Approved", IsActive = true },
                new CarModel { Brand = "Honda", Name = "Civic", Status = "Pending", IsActive = true },
                new CarModel { Brand = "Ford", Name = "Focus", Status = "Approved", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetActiveCarModelsAsync();

            Assert.Single(result);
            Assert.Equal("Toyota", result[0].Brand);
        }

        [Fact]
        public async Task CreateCarModelAsync_InvalidVehicleType_ThrowsBadRequestException()
        {
            var request = new CreateCarModelDTO { Brand = "Toyota", Name = "Camry", VehicleTypeId = 999 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateCarModelAsync(request));
        }

        [Fact]
        public async Task CreateCarModelAsync_Valid_CreatesWithApprovedStatus()
        {
            var request = new CreateCarModelDTO { Brand = "Toyota", Name = "Corolla" };

            var result = await _sut.CreateCarModelAsync(request);

            Assert.True(result);
            var saved = await _dbContext.CarModels.FirstOrDefaultAsync(c => c.Brand == "Toyota" && c.Name == "Corolla");
            Assert.NotNull(saved);
            Assert.Equal("Approved", saved.Status);
        }

        [Fact]
        public async Task UpdateCarModelAsync_NotFound_ReturnsFalse()
        {
            var request = new UpdateCarModelDTO { Brand = "X", Name = "Y", IsActive = true };

            var result = await _sut.UpdateCarModelAsync(999, request);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateCarModelAsync_Valid_UpdatesFields()
        {
            var model = new CarModel { Brand = "Toyota", Name = "Camry", Status = "Approved", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            var request = new UpdateCarModelDTO { Brand = "Toyota", Name = "Camry 2024", IsActive = false };
            var result = await _sut.UpdateCarModelAsync(model.Id, request);

            Assert.True(result);
            var updated = await _dbContext.CarModels.FirstAsync(c => c.Id == model.Id);
            Assert.Equal("Camry 2024", updated.Name);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task DeleteCarModelAsync_NotFound_ReturnsFalse()
        {
            var result = await _sut.DeleteCarModelAsync(999);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteCarModelAsync_Valid_SoftDeletes()
        {
            var model = new CarModel { Brand = "Toyota", Name = "Camry", Status = "Approved", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteCarModelAsync(model.Id);

            Assert.True(result);
            var updated = await _dbContext.CarModels.FirstAsync(c => c.Id == model.Id);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task RequestNewCarModelAsync_InvalidVehicleType_ThrowsBadRequestException()
        {
            var request = new RequestCarModelDTO { Brand = "Toyota", Name = "Prius", VehicleTypeId = 999 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RequestNewCarModelAsync(1, request));
        }

        [Fact]
        public async Task RequestNewCarModelAsync_NoVehicleType_OtherExists_ReusesIt()
        {
            var otherType = new VehicleType { Name = "Other", BaseWeight = 1 };
            _dbContext.VehicleTypes.Add(otherType);
            await _dbContext.SaveChangesAsync();

            var request = new RequestCarModelDTO { Brand = "Tesla", Name = "Model Y" };
            var id = await _sut.RequestNewCarModelAsync(1, request);

            var model = await _dbContext.CarModels.FirstAsync(c => c.Id == id);
            Assert.Equal(otherType.Id, model.VehicleTypeId);

            var typeCount = await _dbContext.VehicleTypes.CountAsync(vt => vt.Name == "Other");
            Assert.Equal(1, typeCount); // not duplicated
        }

        [Fact]
        public async Task RequestNewCarModelAsync_NoVehicleType_NoOtherExists_CreatesOne()
        {
            var request = new RequestCarModelDTO { Brand = "Tesla", Name = "Model 3" };
            var id = await _sut.RequestNewCarModelAsync(1, request);

            var model = await _dbContext.CarModels.FirstAsync(c => c.Id == id);
            var createdType = await _dbContext.VehicleTypes.FirstOrDefaultAsync(vt => vt.Id == model.VehicleTypeId);
            Assert.NotNull(createdType);
            Assert.Equal("Other", createdType.Name);
        }

        [Fact]
        public async Task RequestNewCarModelAsync_CombinesNameVersionYear()
        {
            var request = new RequestCarModelDTO { Brand = "Toyota", Name = "Camry", Version = "XLE", Year = 2024 };
            var id = await _sut.RequestNewCarModelAsync(1, request);

            var model = await _dbContext.CarModels.FirstAsync(c => c.Id == id);
            Assert.Equal("Camry XLE 2024", model.Name);
            Assert.Equal("Pending", model.Status);
            Assert.Equal(1, model.RequestedByUserId);
        }

        [Fact]
        public async Task GetPendingCarModelsAsync_ReturnsOnlyActivePending()
        {
            _dbContext.CarModels.AddRange(
                new CarModel { Brand = "A", Name = "1", Status = "Pending", IsActive = true },
                new CarModel { Brand = "B", Name = "2", Status = "Approved", IsActive = true },
                new CarModel { Brand = "C", Name = "3", Status = "Pending", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetPendingCarModelsAsync();

            Assert.Single(result);
            Assert.Equal("A", result[0].Brand);
        }

        [Fact]
        public async Task ApproveCarModelAsync_NotFound_ThrowsNotFoundException()
        {
            var request = new ApproveCarModelDTO { VehicleTypeId = 1 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ApproveCarModelAsync(999, request));
        }

        [Fact]
        public async Task ApproveCarModelAsync_NotPending_ThrowsBadRequestException()
        {
            var model = new CarModel { Brand = "A", Name = "1", Status = "Approved", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            var request = new ApproveCarModelDTO { VehicleTypeId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ApproveCarModelAsync(model.Id, request));
        }

        [Fact]
        public async Task ApproveCarModelAsync_InvalidVehicleType_ThrowsBadRequestException()
        {
            var model = new CarModel { Brand = "A", Name = "1", Status = "Pending", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            var request = new ApproveCarModelDTO { VehicleTypeId = 999 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ApproveCarModelAsync(model.Id, request));
        }

        [Fact]
        public async Task ApproveCarModelAsync_Valid_ApprovesAndAssignsType()
        {
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var model = new CarModel { Brand = "A", Name = "1", Status = "Pending", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            var request = new ApproveCarModelDTO { VehicleTypeId = vehicleType.Id };
            var result = await _sut.ApproveCarModelAsync(model.Id, request);

            Assert.True(result);
            var updated = await _dbContext.CarModels.FirstAsync(c => c.Id == model.Id);
            Assert.Equal("Approved", updated.Status);
            Assert.Equal(vehicleType.Id, updated.VehicleTypeId);
        }

        [Fact]
        public async Task RejectCarModelAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RejectCarModelAsync(999));
        }

        [Fact]
        public async Task RejectCarModelAsync_NotPending_ThrowsBadRequestException()
        {
            var model = new CarModel { Brand = "A", Name = "1", Status = "Approved", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RejectCarModelAsync(model.Id));
        }

        [Fact]
        public async Task RejectCarModelAsync_Valid_RejectsAndDeactivates()
        {
            var model = new CarModel { Brand = "A", Name = "1", Status = "Pending", IsActive = true };
            _dbContext.CarModels.Add(model);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.RejectCarModelAsync(model.Id);

            Assert.True(result);
            var updated = await _dbContext.CarModels.FirstAsync(c => c.Id == model.Id);
            Assert.Equal("Rejected", updated.Status);
            Assert.False(updated.IsActive);
        }
    }
}