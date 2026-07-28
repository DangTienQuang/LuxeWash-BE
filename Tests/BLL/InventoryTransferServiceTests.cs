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
    public class InventoryTransferServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly InventoryTransferService _sut;

        public InventoryTransferServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new InventoryTransferService(_dbContext);
        }

        private async Task<(User manager, Branch branch)> SeedManager(bool allowNegativeStock = false, decimal? negativeLimit = null)
        {
            var branch = new Branch { Name = "Branch A", IsActive = true, AllowNegativeStock = allowNegativeStock, NegativeStockLimit = negativeLimit };
            _dbContext.Branches.Add(branch);
            var manager = new User { PhoneNumber = "0999800" + new Random().Next(100, 999), Email = $"invmgr{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(manager);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = manager.UserId, FullName = "Inv Mgr", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            return (manager, branch);
        }

        private async Task<Material> SeedMaterial(bool requiresExpiry = false, bool isActive = true)
        {
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = isActive, RequiresExpiryTracking = requiresExpiry, DefaultMinStockLevel = 10 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();
            return material;
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_ManagerNotAssigned_ThrowsBadRequestException()
        {
            var dto = new ImportMaterialBatchDTO { MaterialId = 1, BatchCode = "B1", Quantity = 10, UnitCost = 1000 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportBatchToManagerBranchAsync(999, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_MaterialNotFound_ThrowsNotFoundException()
        {
            var (manager, branch) = await SeedManager();
            var dto = new ImportMaterialBatchDTO { MaterialId = 999, BatchCode = "B1", Quantity = 10, UnitCost = 1000 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_InactiveMaterial_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial(isActive: false);
            var dto = new ImportMaterialBatchDTO { MaterialId = material.MaterialId, BatchCode = "B1", Quantity = 10, UnitCost = 1000 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_RequiresExpiryButMissing_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial(requiresExpiry: true);
            var dto = new ImportMaterialBatchDTO { MaterialId = material.MaterialId, BatchCode = "B1", Quantity = 10, UnitCost = 1000 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_ExpiryInPast_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var dto = new ImportMaterialBatchDTO { MaterialId = material.MaterialId, BatchCode = "B1", Quantity = 10, UnitCost = 1000, ExpiryDate = DateTime.UtcNow.AddDays(-1) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_ManufactureAfterExpiry_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var dto = new ImportMaterialBatchDTO
            {
                MaterialId = material.MaterialId,
                BatchCode = "B1",
                Quantity = 10,
                UnitCost = 1000,
                ManufactureDate = DateTime.UtcNow.AddDays(20),
                ExpiryDate = DateTime.UtcNow.AddDays(10)
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_DuplicateBatchCode_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            _dbContext.MaterialBatches.Add(new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "DUP1", ImportedQuantity = 5, RemainingQuantity = 5, UnitCost = 1000, TotalCost = 5000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var dto = new ImportMaterialBatchDTO { MaterialId = material.MaterialId, BatchCode = "DUP1", Quantity = 10, UnitCost = 1000 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task ImportBatchToManagerBranchAsync_Valid_CreatesWarehouseAndBatchAndStock()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var dto = new ImportMaterialBatchDTO { MaterialId = material.MaterialId, BatchCode = "B2", Quantity = 50, UnitCost = 2000, SupplierName = "Supplier X" };

            var result = await _sut.ImportBatchToManagerBranchAsync(manager.UserId, dto);

            Assert.Equal("B2", result.BatchCode);
            Assert.Equal(50, result.RemainingQuantity);

            var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.BranchId == branch.BranchId);
            Assert.NotNull(warehouse);
            var stock = await _dbContext.WarehouseStocks.FirstOrDefaultAsync(s => s.WarehouseId == warehouse.WarehouseId && s.MaterialId == material.MaterialId);
            Assert.NotNull(stock);
            Assert.Equal(50, stock.CurrentQuantity);
        }

        [Fact]
        public async Task DiscardManagerBatchAsync_NotFoundInBranch_ThrowsNotFoundException()
        {
            var (manager, branch) = await SeedManager();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DiscardManagerBatchAsync(manager.UserId, 999));
        }

        [Fact]
        public async Task DiscardManagerBatchAsync_AlreadyDepleted_MarksDepletedOnly()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B3", ImportedQuantity = 10, RemainingQuantity = 0, UnitCost = 1000, TotalCost = 10000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            await _dbContext.SaveChangesAsync();

            await _sut.DiscardManagerBatchAsync(manager.UserId, batch.MaterialBatchId);

            var updated = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch.MaterialBatchId);
            Assert.Equal("Depleted", updated.Status);
        }

        [Fact]
        public async Task DiscardManagerBatchAsync_StockLowerThanBatch_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B4", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 5, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DiscardManagerBatchAsync(manager.UserId, batch.MaterialBatchId));
        }

        [Fact]
        public async Task DiscardManagerBatchAsync_Valid_DiscardsSuccessfully()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "B5", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 50, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            await _sut.DiscardManagerBatchAsync(manager.UserId, batch.MaterialBatchId, "Expired");

            var updatedBatch = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch.MaterialBatchId);
            Assert.Equal("Discarded", updatedBatch.Status);
            var updatedStock = await _dbContext.WarehouseStocks.FirstAsync(s => s.WarehouseId == warehouse.WarehouseId);
            Assert.Equal(40, updatedStock.CurrentQuantity);
        }

        [Fact]
        public async Task AdjustManagerStockAsync_ZeroChange_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var dto = new AdjustBranchInventoryDTO { MaterialId = 1, QuantityChange = 0 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AdjustManagerStockAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AdjustManagerStockAsync_MaterialNotFound_ThrowsNotFoundException()
        {
            var (manager, branch) = await SeedManager();
            var dto = new AdjustBranchInventoryDTO { MaterialId = 999, QuantityChange = 5 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.AdjustManagerStockAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AdjustManagerStockAsync_InactiveMaterial_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial(isActive: false);
            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = 5 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AdjustManagerStockAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AdjustManagerStockAsync_PositiveChangeNoBatch_CreatesNewBatch()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = 20, Reason = "restock" };

            var result = await _sut.AdjustManagerStockAsync(manager.UserId, dto);

            Assert.Equal(20, result.CurrentQuantity);

            var batch = await _dbContext.MaterialBatches.FirstOrDefaultAsync(b => b.MaterialId == material.MaterialId);
            Assert.NotNull(batch);
            Assert.Equal(20, batch.RemainingQuantity);
        }

        [Fact]
        public async Task AdjustManagerStockAsync_PositiveChangeExistingBatch_IncrementsBatch()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "EXIST1", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 10, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = 5, MaterialBatchId = batch.MaterialBatchId };
            var result = await _sut.AdjustManagerStockAsync(manager.UserId, dto);

            Assert.Equal(15, result.CurrentQuantity);
            var updatedBatch = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch.MaterialBatchId);
            Assert.Equal(15, updatedBatch.RemainingQuantity);
        }

        [Fact]
        public async Task AdjustManagerStockAsync_NegativeChangeWithBatch_DecrementsBatch()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "EXIST2", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 10, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = -4, MaterialBatchId = batch.MaterialBatchId };
            var result = await _sut.AdjustManagerStockAsync(manager.UserId, dto);

            Assert.Equal(6, result.CurrentQuantity);
            var updatedBatch = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch.MaterialBatchId);
            Assert.Equal(6, updatedBatch.RemainingQuantity);
        }

        [Fact]
        public async Task AdjustManagerStockAsync_NegativeChangeWithBatch_ExceedsBatchRemaining_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "EXIST3", ImportedQuantity = 5, RemainingQuantity = 5, UnitCost = 1000, TotalCost = 5000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 5, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = -10, MaterialBatchId = batch.MaterialBatchId };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AdjustManagerStockAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AdjustManagerStockAsync_NegativeChangeNoBatch_FIFOAcrossMultipleBatches()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            var batch1 = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "FIFO1", ImportedQuantity = 5, RemainingQuantity = 5, UnitCost = 1000, TotalCost = 5000, Status = "Active", ImportedAt = DateTime.UtcNow.AddDays(-2) };
            var batch2 = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "FIFO2", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1200, TotalCost = 12000, Status = "Active", ImportedAt = DateTime.UtcNow.AddDays(-1) };
            _dbContext.MaterialBatches.AddRange(batch1, batch2);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 15, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = -8 }; // no MaterialBatchId — FIFO

            var result = await _sut.AdjustManagerStockAsync(manager.UserId, dto);

            Assert.Equal(7, result.CurrentQuantity); // 15 - 8

            var updatedBatch1 = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch1.MaterialBatchId);
            var updatedBatch2 = await _dbContext.MaterialBatches.FirstAsync(b => b.MaterialBatchId == batch2.MaterialBatchId);
            Assert.Equal(0, updatedBatch1.RemainingQuantity); // depleted first (oldest)
            Assert.Equal("Depleted", updatedBatch1.Status);
            Assert.Equal(7, updatedBatch2.RemainingQuantity); // 10 - 3
        }

        [Fact]
        public async Task AdjustManagerStockAsync_NegativeChangeExceedsAllBatches_NoNegativeAllowed_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager(allowNegativeStock: false);
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();
            var batch = new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "SMALL1", ImportedQuantity = 3, RemainingQuantity = 3, UnitCost = 1000, TotalCost = 3000, Status = "Active" };
            _dbContext.MaterialBatches.Add(batch);
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 3, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = -10 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AdjustManagerStockAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AdjustManagerStockAsync_NegativeResultExceedsStockLimit_ThrowsBadRequestException()
        {
            var (manager, branch) = await SeedManager(allowNegativeStock: true, negativeLimit: 5);
            var material = await SeedMaterial();
            var dto = new AdjustBranchInventoryDTO { MaterialId = material.MaterialId, QuantityChange = -20 }; // stock starts at 0, would go to -20, exceeds -5 limit

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AdjustManagerStockAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task GetManagerStocksAsync_ManagerNotAssigned_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetManagerStocksAsync(999));
        }

        [Fact]
        public async Task GetManagerStocksAsync_ReturnsOnlyManagerBranchStocks()
        {
            var (manager, branch) = await SeedManager();
            var otherBranch = new Branch { Name = "Other", IsActive = true };
            _dbContext.Branches.Add(otherBranch);
            var material = await SeedMaterial();
            var wh1 = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            var wh2 = new Warehouse { Name = "Kho B", Type = "Branch", BranchId = otherBranch.BranchId, IsActive = true };
            _dbContext.Warehouses.AddRange(wh1, wh2);
            await _dbContext.SaveChangesAsync();

            _dbContext.WarehouseStocks.AddRange(
                new WarehouseStock { WarehouseId = wh1.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 10, UpdatedAt = DateTime.UtcNow },
                new WarehouseStock { WarehouseId = wh2.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 20, UpdatedAt = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerStocksAsync(manager.UserId);

            Assert.Single(result);
            Assert.Equal(10, result[0].CurrentQuantity);
        }

        [Fact]
        public async Task GetManagerBatchesAsync_FiltersByMaterialAndExcludesDepletedByDefault()
        {
            var (manager, branch) = await SeedManager();
            var material1 = await SeedMaterial();
            var material2 = new Material { Name = "Wax", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material2);
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.MaterialBatches.AddRange(
                new MaterialBatch { MaterialId = material1.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "M1", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active" },
                new MaterialBatch { MaterialId = material1.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "M1D", ImportedQuantity = 10, RemainingQuantity = 0, UnitCost = 1000, TotalCost = 10000, Status = "Depleted" },
                new MaterialBatch { MaterialId = material2.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "M2", ImportedQuantity = 5, RemainingQuantity = 5, UnitCost = 500, TotalCost = 2500, Status = "Active" }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerBatchesAsync(manager.UserId, materialId: material1.MaterialId);

            Assert.Single(result);
            Assert.Equal("M1", result[0].BatchCode);
        }

        [Fact]
        public async Task GetManagerBatchesAsync_IncludeDepleted_ReturnsAll()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.MaterialBatches.AddRange(
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "M3", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active" },
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "M3D", ImportedQuantity = 10, RemainingQuantity = 0, UnitCost = 1000, TotalCost = 10000, Status = "Depleted" }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerBatchesAsync(manager.UserId, includeDepleted: true);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetManagerExpiringBatchesAsync_ReturnsOnlyWithinWarningWindow()
        {
            var (manager, branch) = await SeedManager();
            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true, ExpiryWarningDays = 10 };
            _dbContext.Materials.Add(material);
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.MaterialBatches.AddRange(
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "EXP1", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active", ExpiryDate = DateTime.UtcNow.AddDays(5) },
                new MaterialBatch { MaterialId = material.MaterialId, WarehouseId = warehouse.WarehouseId, BatchCode = "EXP2", ImportedQuantity = 10, RemainingQuantity = 10, UnitCost = 1000, TotalCost = 10000, Status = "Active", ExpiryDate = DateTime.UtcNow.AddDays(60) }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerExpiringBatchesAsync(manager.UserId);

            Assert.Single(result);
            Assert.Equal("EXP1", result[0].BatchCode);
        }

        [Fact]
        public async Task GetManagerTransactionsAsync_FiltersByType()
        {
            var (manager, branch) = await SeedManager();
            var material = await SeedMaterial();
            var warehouse = new Warehouse { Name = "Kho A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            _dbContext.InventoryTransactions.AddRange(
                new InventoryTransaction { WarehouseId = warehouse.WarehouseId, BranchId = branch.BranchId, MaterialId = material.MaterialId, TransactionType = "BranchImport", Quantity = 10, UnitCost = 1000, CostAmount = 10000, BeforeQuantity = 0, AfterQuantity = 10, CreatedAt = DateTime.UtcNow },
                new InventoryTransaction { WarehouseId = warehouse.WarehouseId, BranchId = branch.BranchId, MaterialId = material.MaterialId, TransactionType = "Discard", Quantity = 5, UnitCost = 1000, CostAmount = 5000, BeforeQuantity = 10, AfterQuantity = 5, CreatedAt = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerTransactionsAsync(manager.UserId, type: "Discard");

            Assert.Single(result);
            Assert.Equal("Discard", result[0].TransactionType);
        }

        [Fact]
        public async Task GetBranchInventorySettingAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBranchInventorySettingAsync(999));
        }

        [Fact]
        public async Task GetBranchInventorySettingAsync_Valid_ReturnsSettings()
        {
            var (manager, branch) = await SeedManager(allowNegativeStock: true, negativeLimit: 50);

            var result = await _sut.GetBranchInventorySettingAsync(branch.BranchId);

            Assert.True(result.AllowNegativeStock);
            Assert.Equal(50, result.NegativeStockLimit);
        }

        [Fact]
        public async Task UpdateBranchInventorySettingAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateBranchInventorySettingDTO { AllowNegativeStock = true, NegativeStockLimit = 100 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateBranchInventorySettingAsync(999, dto));
        }

        [Fact]
        public async Task UpdateBranchInventorySettingAsync_Valid_UpdatesSettings()
        {
            var (manager, branch) = await SeedManager();

            var dto = new UpdateBranchInventorySettingDTO { AllowNegativeStock = true, NegativeStockLimit = 200 };
            var result = await _sut.UpdateBranchInventorySettingAsync(branch.BranchId, dto);

            Assert.True(result.AllowNegativeStock);
            Assert.Equal(200, result.NegativeStockLimit);
        }
    }
}