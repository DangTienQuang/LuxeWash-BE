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

        [Fact]
        public async Task GetStocksAsync_NoBranchFilter_ReturnsAll()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true, DefaultMinStockLevel = 10 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 50, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStocksAsync();

            Assert.Single(result);
        }

        [Fact]
        public async Task GetStocksAsync_FilterByBranch_ReturnsOnlyMatching()
        {
            var branch1 = new Branch { Name = "Branch A", IsActive = true };
            var branch2 = new Branch { Name = "Branch B", IsActive = true };
            _dbContext.Branches.AddRange(branch1, branch2);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true, DefaultMinStockLevel = 10 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var wh1 = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch1.BranchId, IsActive = true };
            var wh2 = new Warehouse { Name = "Kho B", Type = "Branch", BranchId = branch2.BranchId, IsActive = true };
            _dbContext.Warehouses.AddRange(wh1, wh2);
            await _dbContext.SaveChangesAsync();

            _dbContext.WarehouseStocks.AddRange(
                new WarehouseStock { WarehouseId = wh1.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 50, UpdatedAt = DateTime.UtcNow },
                new WarehouseStock { WarehouseId = wh2.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 30, UpdatedAt = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStocksAsync(branch1.BranchId);

            Assert.Single(result);
            Assert.Equal(branch1.BranchId, result[0].BranchId);
        }

        [Fact]
        public async Task GetStocksAsync_ComputesIsLowStockCorrectly()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true, DefaultMinStockLevel = 20 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 5, UpdatedAt = DateTime.UtcNow }); // below default min of 20
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStocksAsync();

            Assert.True(result[0].IsLowStock);
        }

        [Fact]
        public async Task GetBatchesAsync_ExpiringOnlyFalse_ReturnsAll()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true, ExpiryWarningDays = 30 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.MaterialBatches.AddRange(
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B1", ImportedQuantity = 50, RemainingQuantity = 50, UnitCost = 1000, TotalCost = 50000, Status = "Active", ExpiryDate = DateTime.UtcNow.AddDays(100) },
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B2", ImportedQuantity = 20, RemainingQuantity = 20, UnitCost = 1000, TotalCost = 20000, Status = "Active", ExpiryDate = DateTime.UtcNow.AddDays(5) }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBatchesAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetBatchesAsync_ExpiringOnlyTrue_ReturnsOnlyWithinWarningWindow()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true, ExpiryWarningDays = 10 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.MaterialBatches.AddRange(
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B1", ImportedQuantity = 50, RemainingQuantity = 50, UnitCost = 1000, TotalCost = 50000, Status = "Active", ExpiryDate = DateTime.UtcNow.AddDays(100) }, // far away
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B2", ImportedQuantity = 20, RemainingQuantity = 20, UnitCost = 1000, TotalCost = 20000, Status = "Active", ExpiryDate = DateTime.UtcNow.AddDays(5) } // within 10-day window
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBatchesAsync(expiringOnly: true);

            Assert.Single(result);
            Assert.Equal("B2", result[0].BatchCode);
        }

        [Fact]
        public async Task DiscardBatchAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DiscardBatchAsync(999));
        }

        [Fact]
        public async Task DiscardBatchAsync_AlreadyDepleted_MarksDepletedNoStockChange()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B1", ImportedQuantity = 50, RemainingQuantity = 0, UnitCost = 1000, TotalCost = 50000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            await _dbContext.SaveChangesAsync();

            await _sut.DiscardBatchAsync(batch.MaterialBatchId);

            var updated = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch.MaterialBatchId);
            Assert.Equal("Depleted", updated.Status);
        }

        [Fact]
        public async Task DiscardBatchAsync_StockLowerThanBatch_ThrowsBadRequestException()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B1", ImportedQuantity = 50, RemainingQuantity = 30, UnitCost = 1000, TotalCost = 50000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 10, UpdatedAt = DateTime.UtcNow }); // less than batch's 30
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DiscardBatchAsync(batch.MaterialBatchId));
        }

        [Fact]
        public async Task DiscardBatchAsync_Valid_DiscardsAndCreatesTransaction()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B1", ImportedQuantity = 50, RemainingQuantity = 20, UnitCost = 1000, TotalCost = 50000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 100, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            await _sut.DiscardBatchAsync(batch.MaterialBatchId, "Expired");

            var updatedBatch = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch.MaterialBatchId);
            Assert.Equal(0, updatedBatch.RemainingQuantity);
            Assert.Equal("Discarded", updatedBatch.Status);

            var updatedStock = await _dbContext.WarehouseStocks.FirstAsync(s => s.WarehouseId == warehouse.WarehouseId);
            Assert.Equal(80, updatedStock.CurrentQuantity); // 100 - 20

            var tx = await _dbContext.InventoryTransactions.FirstOrDefaultAsync(t => t.MaterialBatchId == batch.MaterialBatchId);
            Assert.NotNull(tx);
            Assert.Equal("Discard", tx.TransactionType);
            Assert.Equal("Expired", tx.Note);
        }
    }
}