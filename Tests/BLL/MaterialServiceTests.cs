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
    public class MaterialServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly MaterialService _sut;

        public MaterialServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new MaterialService(_dbContext);
        }

        private async Task<MaterialUnit> SeedUnit(string code = "l", string displayName = "Liter", string measurementType = "Volume")
        {
            var unit = new MaterialUnit { Code = code, DisplayName = displayName, MeasurementType = measurementType, IsActive = true };
            _dbContext.MaterialUnits.Add(unit);
            await _dbContext.SaveChangesAsync();
            return unit;
        }

        [Fact]
        public async Task GetMaterialsAsync_ExcludesInactiveByDefault()
        {
            _dbContext.Materials.AddRange(
                new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true },
                new Material { Name = "Old Wax", Category = "Chemical", Unit = "liter", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetMaterialsAsync();

            Assert.Single(result);
        }

        [Fact]
        public async Task GetMaterialsAsync_IncludeInactive_ReturnsAll()
        {
            _dbContext.Materials.AddRange(
                new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true },
                new Material { Name = "Old Wax", Category = "Chemical", Unit = "liter", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetMaterialsAsync(includeInactive: true);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task CreateMaterialUnitAsync_DuplicateCode_ThrowsBadRequestException()
        {
            await SeedUnit(code: "l");

            var dto = new CreateMaterialUnitDTO { Code = "L", DisplayName = "Liter Again", MeasurementType = "Volume" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateMaterialUnitAsync(dto));
        }

        [Fact]
        public async Task CreateMaterialUnitAsync_InvalidCodeFormat_ThrowsBadRequestException()
        {
            var dto = new CreateMaterialUnitDTO { Code = "L!TER", DisplayName = "Bad", MeasurementType = "Volume" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateMaterialUnitAsync(dto));
        }

        [Fact]
        public async Task CreateMaterialUnitAsync_Valid_Creates()
        {
            var dto = new CreateMaterialUnitDTO { Code = "kg", DisplayName = "Kilogram", MeasurementType = "Weight" };

            var result = await _sut.CreateMaterialUnitAsync(dto);

            Assert.Equal("kg", result.Code);
        }

        [Fact]
        public async Task UpdateMaterialUnitAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateMaterialUnitDTO { DisplayName = "X", MeasurementType = "Y", IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateMaterialUnitAsync(999, dto));
        }

        [Fact]
        public async Task UpdateMaterialUnitAsync_DeactivateInUse_ThrowsBadRequestException()
        {
            var unit = await SeedUnit(code: "l");
            _dbContext.Materials.Add(new Material { Name = "Shampoo", Category = "Chemical", Unit = "l", IsActive = true });
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateMaterialUnitDTO { DisplayName = "Liter", MeasurementType = "Volume", IsActive = false };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateMaterialUnitAsync(unit.UnitId, dto));
        }

        [Fact]
        public async Task UpdateMaterialUnitAsync_Valid_Updates()
        {
            var unit = await SeedUnit(code: "kg");

            var dto = new UpdateMaterialUnitDTO { DisplayName = "Kilos", MeasurementType = "Weight", IsActive = true };
            var result = await _sut.UpdateMaterialUnitAsync(unit.UnitId, dto);

            Assert.Equal("Kilos", result.DisplayName);
        }

        [Fact]
        public async Task CreateMaterialAsync_UnitNotRecognized_ThrowsBadRequestException()
        {
            var dto = new CreateMaterialDTO { Name = "Shampoo", Category = "Chemical", Unit = "bogus" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateMaterialAsync(dto));
        }

        [Fact]
        public async Task CreateMaterialAsync_ValidWithAlias_NormalizesUnit()
        {
            await SeedUnit(code: "liter", displayName: "Liter", measurementType: "Volume");

            var dto = new CreateMaterialDTO { Name = "Shampoo", Category = "Chemical", Unit = "L" }; // alias for "liter"

            var result = await _sut.CreateMaterialAsync(dto);

            Assert.Equal("liter", result.Unit);
        }

        [Fact]
        public async Task UpdateMaterialAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateMaterialDTO { Name = "X", Category = "Y", Unit = "liter", IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateMaterialAsync(999, dto));
        }

        [Fact]
        public async Task UpdateMaterialAsync_SameUnit_UpdatesWithoutValidation()
        {
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateMaterialDTO { Name = "Shampoo Deluxe", Category = "Chemical", Unit = "liter", IsActive = true };
            var result = await _sut.UpdateMaterialAsync(material.MaterialId, dto);

            Assert.Equal("Shampoo Deluxe", result.Name);
        }

        [Fact]
        public async Task UpdateMaterialAsync_ChangeToInvalidUnit_ThrowsBadRequestException()
        {
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateMaterialDTO { Name = "Shampoo", Category = "Chemical", Unit = "bogus", IsActive = true };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateMaterialAsync(material.MaterialId, dto));
        }

        [Fact]
        public async Task UpdateMaterialAsync_ChangeUnitWithHistory_ThrowsBadRequestException()
        {
            await SeedUnit(code: "kilogram", displayName: "Kilogram", measurementType: "Weight");
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage { ServiceId = 1, MaterialId = material.MaterialId, BaseQuantity = 1, Unit = "liter", IsActive = true });
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateMaterialDTO { Name = "Shampoo", Category = "Chemical", Unit = "kg", IsActive = true };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateMaterialAsync(material.MaterialId, dto));
        }

        [Fact]
        public async Task UpdateMaterialAsync_ChangeUnitNoHistory_Succeeds()
        {
            await SeedUnit(code: "kilogram", displayName: "Kilogram", measurementType: "Weight");
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateMaterialDTO { Name = "Shampoo", Category = "Chemical", Unit = "kg", IsActive = true };
            var result = await _sut.UpdateMaterialAsync(material.MaterialId, dto);

            Assert.Equal("kilogram", result.Unit);
        }
    }
}