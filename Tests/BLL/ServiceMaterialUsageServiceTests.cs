using System;
using System.Collections.Generic;
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
    public class ServiceMaterialUsageServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly ServiceMaterialUsageService _sut;

        public ServiceMaterialUsageServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new ServiceMaterialUsageService(_dbContext);
        }

        private async Task<(Service service, Material material)> SeedServiceAndMaterial()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();
            return (service, material);
        }

        [Fact]
        public async Task GetByServiceAsync_ServiceNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByServiceAsync(999));
        }

        [Fact]
        public async Task GetByServiceAsync_ReturnsUsages()
        {
            var (service, material) = await SeedServiceAndMaterial();
            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetByServiceAsync(service.ServiceId);

            Assert.Single(result);
            Assert.Equal("Shampoo", result[0].MaterialName);
        }

        [Fact]
        public async Task UpsertAsync_NoItemsNoSingleFields_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var dto = new UpsertServiceMaterialUsageDTO();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpsertAsync(service.ServiceId, dto));
        }

        [Fact]
        public async Task UpsertAsync_SingleFieldsBaseQuantityZero_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 0 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpsertAsync(service.ServiceId, dto));
        }

        [Fact]
        public async Task UpsertAsync_InvalidVehicleType_ThrowsNotFoundException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 2, VehicleTypeId = 999 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpsertAsync(service.ServiceId, dto));
        }

        [Fact]
        public async Task UpsertAsync_DuplicateMaterialInItems_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var dto = new UpsertServiceMaterialUsageDTO
            {
                Items = new List<UpsertServiceMaterialUsageItemDTO>
                {
                    new UpsertServiceMaterialUsageItemDTO { MaterialId = material.MaterialId, BaseQuantity = 1 },
                    new UpsertServiceMaterialUsageItemDTO { MaterialId = material.MaterialId, BaseQuantity = 2 }
                }
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpsertAsync(service.ServiceId, dto));
        }

        [Fact]
        public async Task UpsertAsync_MaterialNotFound_ThrowsNotFoundException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = 999, BaseQuantity = 2 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpsertAsync(service.ServiceId, dto));
        }

        [Fact]
        public async Task UpsertAsync_InactiveMaterial_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            material.IsActive = false;
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 2 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpsertAsync(service.ServiceId, dto));
        }

        [Fact]
        public async Task UpsertAsync_NewUsage_CreatesEntry()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 3 };

            var result = await _sut.UpsertAsync(service.ServiceId, dto);

            Assert.Single(result);
            Assert.Equal(3, result[0].BaseQuantity);
        }

        [Fact]
        public async Task UpsertAsync_ExistingUsage_UpdatesInPlaceNotDuplicated()
        {
            var (service, material) = await SeedServiceAndMaterial();
            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true });
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 5 };
            var result = await _sut.UpsertAsync(service.ServiceId, dto);

            Assert.Single(result);
            Assert.Equal(5, result[0].BaseQuantity);
        }

        [Fact]
        public async Task UpdateAsync_UsageNotFound_ThrowsNotFoundException()
        {
            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = 1, BaseQuantity = 2 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateAsync(1, 999, dto));
        }

        [Fact]
        public async Task UpdateAsync_MissingRequiredFields_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var usage = new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true };
            _dbContext.ServiceMaterialUsages.Add(usage);
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO(); // no MaterialId/BaseQuantity

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateAsync(service.ServiceId, usage.ServiceMaterialUsageId, dto));
        }

        [Fact]
        public async Task UpdateAsync_BaseQuantityZero_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var usage = new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true };
            _dbContext.ServiceMaterialUsages.Add(usage);
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 0 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateAsync(service.ServiceId, usage.ServiceMaterialUsageId, dto));
        }

        [Fact]
        public async Task UpdateAsync_MaterialNotFound_ThrowsNotFoundException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var usage = new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true };
            _dbContext.ServiceMaterialUsages.Add(usage);
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = 999, BaseQuantity = 2 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateAsync(service.ServiceId, usage.ServiceMaterialUsageId, dto));
        }

        [Fact]
        public async Task UpdateAsync_DuplicateAgainstAnotherUsage_ThrowsBadRequestException()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var material2 = new Material { Name = "Wax", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material2);
            await _dbContext.SaveChangesAsync();

            var usage1 = new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true };
            var usage2 = new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material2.MaterialId, BaseQuantity = 1, Unit = "liter", IsActive = true };
            _dbContext.ServiceMaterialUsages.AddRange(usage1, usage2);
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 3 }; // trying to make usage2 use material1's material+vehicletype combo

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateAsync(service.ServiceId, usage2.ServiceMaterialUsageId, dto));
        }

        [Fact]
        public async Task UpdateAsync_Valid_Updates()
        {
            var (service, material) = await SeedServiceAndMaterial();
            var usage = new ServiceMaterialUsage { ServiceId = service.ServiceId, MaterialId = material.MaterialId, BaseQuantity = 2, Unit = "liter", IsActive = true };
            _dbContext.ServiceMaterialUsages.Add(usage);
            await _dbContext.SaveChangesAsync();

            var dto = new UpsertServiceMaterialUsageDTO { MaterialId = material.MaterialId, BaseQuantity = 7 };
            var result = await _sut.UpdateAsync(service.ServiceId, usage.ServiceMaterialUsageId, dto);

            Assert.Equal(7, result.BaseQuantity);
        }

        [Fact]
        public async Task GetConditionMultipliersAsync_SeedsDefaultsWhenEmpty()
        {
            var result = await _sut.GetConditionMultipliersAsync();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetConditionMultipliersAsync_DoesNotDuplicateOnSecondCall()
        {
            await _sut.GetConditionMultipliersAsync();
            var result = await _sut.GetConditionMultipliersAsync();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task UpdateConditionMultiplierAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateVehicleConditionMaterialMultiplierDTO { Multiplier = 2.5m, IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateConditionMultiplierAsync(999, dto));
        }

        [Fact]
        public async Task UpdateConditionMultiplierAsync_Valid_Updates()
        {
            var multiplier = new VehicleConditionMaterialMultiplier { VehicleCondition = VehicleCondition.Dirty, Multiplier = 1.5m, IsActive = true };
            _dbContext.VehicleConditionMaterialMultipliers.Add(multiplier);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateVehicleConditionMaterialMultiplierDTO { Multiplier = 1.8m, Description = "updated", IsActive = false };
            var result = await _sut.UpdateConditionMultiplierAsync(multiplier.Id, dto);

            Assert.Equal(1.8m, result.Multiplier);
            Assert.False(result.IsActive);
        }
    }
}