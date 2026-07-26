using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class VehicleTypeServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly VehicleTypeService _sut;

        public VehicleTypeServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new VehicleTypeService(_dbContext);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllMapped()
        {
            _dbContext.VehicleTypes.AddRange(
                new VehicleType { Name = "Sedan", BaseWeight = 3 },
                new VehicleType { Name = "SUV", BaseWeight = 5 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task CreateAsync_DuplicateNameCaseInsensitive_ThrowsBadRequestException()
        {
            _dbContext.VehicleTypes.Add(new VehicleType { Name = "Sedan", BaseWeight = 3 });
            await _dbContext.SaveChangesAsync();

            var request = new CreateVehicleTypeDTO { Name = "SEDAN", BaseWeight = 4 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateAsync(request));
        }

        [Fact]
        public async Task CreateAsync_Valid_CreatesAndTrimsName()
        {
            var request = new CreateVehicleTypeDTO { Name = "  Truck  ", Description = "Big vehicle", BaseWeight = 8 };

            var result = await _sut.CreateAsync(request);

            Assert.Equal("Truck", result.Name);
            var saved = await _dbContext.VehicleTypes.FirstOrDefaultAsync(t => t.Id == result.Id);
            Assert.NotNull(saved);
        }

        [Fact]
        public async Task UpdateAsync_NotFound_ThrowsNotFoundException()
        {
            var request = new CreateVehicleTypeDTO { Name = "X", BaseWeight = 1 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateAsync(999, request));
        }

        [Fact]
        public async Task UpdateAsync_DuplicateNameAgainstDifferentType_ThrowsBadRequestException()
        {
            var typeA = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            var typeB = new VehicleType { Name = "SUV", BaseWeight = 5 };
            _dbContext.VehicleTypes.AddRange(typeA, typeB);
            await _dbContext.SaveChangesAsync();

            var request = new CreateVehicleTypeDTO { Name = "Sedan", BaseWeight = 5 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateAsync(typeB.Id, request));
        }

        [Fact]
        public async Task UpdateAsync_Valid_UpdatesFields()
        {
            var type = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(type);
            await _dbContext.SaveChangesAsync();

            var request = new CreateVehicleTypeDTO { Name = "Sedan Plus", Description = "Updated", BaseWeight = 4 };
            var result = await _sut.UpdateAsync(type.Id, request);

            Assert.True(result);
            var updated = await _dbContext.VehicleTypes.FirstAsync(t => t.Id == type.Id);
            Assert.Equal("Sedan Plus", updated.Name);
            Assert.Equal(4, updated.BaseWeight);
        }

        [Fact]
        public async Task DeleteAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAsync(999));
        }

        [Fact]
        public async Task DeleteAsync_HasVehicles_ThrowsBadRequestException()
        {
            var type = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(type);
            await _dbContext.SaveChangesAsync();

            _dbContext.Vehicles.Add(new Vehicle { LicensePlate = "51Z11111", VehicleTypeId = type.Id });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAsync(type.Id));
        }

        [Fact]
        public async Task DeleteAsync_NoVehicles_DeletesSuccessfully()
        {
            var type = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(type);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteAsync(type.Id);

            Assert.True(result);
            var stillExists = await _dbContext.VehicleTypes.AnyAsync(t => t.Id == type.Id);
            Assert.False(stillExists);
        }
    }
}