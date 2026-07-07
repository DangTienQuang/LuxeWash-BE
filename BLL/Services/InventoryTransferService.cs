using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AutoWashPro.BLL.Services
{
    public class InventoryTransferService : IInventoryTransferService
    {
        private readonly AutoWashDbContext _context;

        public InventoryTransferService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<WarehouseStockDTO>> GetManagerStocksAsync(int managerUserId)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var stocks = await QueryStocks()
                .Where(s => s.Warehouse.BranchId == branchId)
                .OrderBy(s => s.Material.Name)
                .ToListAsync();
            return stocks.Select(MapStock).ToList();
        }

        public async Task<List<MaterialBatchDTO>> GetManagerBatchesAsync(int managerUserId, int? materialId = null, bool includeDepleted = false)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var batches = await QueryBatches()
                .Where(b => b.Warehouse.BranchId == branchId)
                .Where(b => materialId == null || b.MaterialId == materialId.Value)
                .Where(b => includeDepleted || b.RemainingQuantity > 0)
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.Material.Name)
                .ToListAsync();
            return batches.Select(MapBatch).ToList();
        }

        public async Task<List<MaterialBatchDTO>> GetManagerExpiringBatchesAsync(int managerUserId)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var today = DateTime.UtcNow.Date;
            var batches = await QueryBatches()
                .Where(b => b.Warehouse.BranchId == branchId
                    && b.ExpiryDate != null
                    && b.RemainingQuantity > 0
                    && b.ExpiryDate.Value.Date <= today.AddDays(b.Material.ExpiryWarningDays))
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync();
            return batches.Select(MapBatch).ToList();
        }

        public async Task<MaterialBatchDTO> ImportBatchToManagerBranchAsync(int managerUserId, ImportMaterialBatchDTO dto)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var material = await ValidateMaterialForImportAsync(dto);
            var warehouse = await GetOrCreateBranchWarehouseAsync(branchId);
            var manufactureDate = NormalizeDate(dto.ManufactureDate);
            var expiryDate = NormalizeDate(dto.ExpiryDate);

            if (await _context.MaterialBatches.AnyAsync(b => b.BatchCode == dto.BatchCode.Trim()))
            {
                throw new BadRequestException("Batch code already exists.");
            }

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var batch = new MaterialBatch
            {
                MaterialId = material.MaterialId,
                WarehouseId = warehouse.WarehouseId,
                BatchCode = dto.BatchCode.Trim(),
                ImportedQuantity = dto.Quantity,
                RemainingQuantity = dto.Quantity,
                UnitCost = dto.UnitCost,
                TotalCost = dto.Quantity * dto.UnitCost,
                ManufactureDate = manufactureDate,
                ExpiryDate = expiryDate,
                SupplierName = dto.SupplierName,
                Status = "Active",
                ImportedAt = DateTime.UtcNow
            };
            _context.MaterialBatches.Add(batch);
            await _context.SaveChangesAsync();

            var stock = await GetOrCreateStockAsync(warehouse.WarehouseId, material.MaterialId, material.DefaultMinStockLevel);
            var before = stock.CurrentQuantity;
            stock.CurrentQuantity += dto.Quantity;
            stock.UpdatedAt = DateTime.UtcNow;

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                WarehouseId = warehouse.WarehouseId,
                BranchId = branchId,
                MaterialId = material.MaterialId,
                MaterialBatchId = batch.MaterialBatchId,
                TransactionType = "BranchImport",
                Quantity = dto.Quantity,
                UnitCost = dto.UnitCost,
                CostAmount = dto.Quantity * dto.UnitCost,
                BeforeQuantity = before,
                AfterQuantity = stock.CurrentQuantity,
                CreatedByUserId = managerUserId,
                Note = $"Manager imported batch {batch.BatchCode}"
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return await GetBatchDtoAsync(batch.MaterialBatchId);
        }

        public async Task DiscardManagerBatchAsync(int managerUserId, int batchId, string? reason = null)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var batch = await _context.MaterialBatches
                .Include(b => b.Warehouse)
                .FirstOrDefaultAsync(b => b.MaterialBatchId == batchId && b.Warehouse.BranchId == branchId)
                ?? throw new NotFoundException("Batch not found in your branch warehouse.");

            if (batch.RemainingQuantity <= 0)
            {
                batch.Status = "Depleted";
                await _context.SaveChangesAsync();
                return;
            }

            var stock = await _context.WarehouseStocks
                .FirstOrDefaultAsync(s => s.WarehouseId == batch.WarehouseId && s.MaterialId == batch.MaterialId)
                ?? throw new BadRequestException("Warehouse stock not found.");

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var before = stock.CurrentQuantity;
            var discardQuantity = batch.RemainingQuantity;

            if (stock.CurrentQuantity < discardQuantity)
            {
                throw new BadRequestException("Warehouse stock is lower than the batch remaining quantity. Please adjust stock first.");
            }

            stock.CurrentQuantity -= discardQuantity;
            stock.UpdatedAt = DateTime.UtcNow;
            batch.RemainingQuantity = 0;
            batch.Status = "Discarded";

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                WarehouseId = batch.WarehouseId,
                BranchId = branchId,
                MaterialId = batch.MaterialId,
                MaterialBatchId = batch.MaterialBatchId,
                TransactionType = "Discard",
                Quantity = discardQuantity,
                UnitCost = batch.UnitCost,
                CostAmount = discardQuantity * batch.UnitCost,
                BeforeQuantity = before,
                AfterQuantity = stock.CurrentQuantity,
                CreatedByUserId = managerUserId,
                Note = reason ?? "Manager discarded material batch"
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task<WarehouseStockDTO> AdjustManagerStockAsync(int managerUserId, AdjustBranchInventoryDTO dto)
        {
            if (dto.QuantityChange == 0) throw new BadRequestException("Quantity change must be different from 0.");

            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var branch = await _context.Branches.FindAsync(branchId) ?? throw new NotFoundException("Branch not found.");
            var material = await _context.Materials.FindAsync(dto.MaterialId) ?? throw new NotFoundException("Material not found.");
            if (!material.IsActive) throw new BadRequestException("Material is inactive.");

            var warehouse = await GetOrCreateBranchWarehouseAsync(branchId);
            var stock = await GetOrCreateStockAsync(warehouse.WarehouseId, material.MaterialId, material.DefaultMinStockLevel);

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var before = stock.CurrentQuantity;
            var after = before + dto.QuantityChange;
            if (after < 0 && !branch.AllowNegativeStock)
            {
                throw new BadRequestException("Adjustment would make stock negative. Enable negative stock or use a smaller adjustment.");
            }
            if (branch.AllowNegativeStock && branch.NegativeStockLimit.HasValue && after < -branch.NegativeStockLimit.Value)
            {
                throw new BadRequestException($"Negative stock limit exceeded. Limit: -{branch.NegativeStockLimit.Value}; projected: {after}.");
            }

            MaterialBatch? batch = null;
            decimal? adjustmentUnitCost = null;
            if (dto.MaterialBatchId.HasValue)
            {
                batch = await _context.MaterialBatches
                    .FirstOrDefaultAsync(b => b.MaterialBatchId == dto.MaterialBatchId.Value
                        && b.WarehouseId == warehouse.WarehouseId
                        && b.MaterialId == material.MaterialId)
                    ?? throw new NotFoundException("Material batch not found in your branch warehouse.");
            }

            if (dto.QuantityChange > 0)
            {
                if (batch == null)
                {
                    var estimatedUnitCost = await EstimateUnitCostAsync(material.MaterialId);
                    batch = new MaterialBatch
                    {
                        MaterialId = material.MaterialId,
                        WarehouseId = warehouse.WarehouseId,
                        BatchCode = $"ADJ-{material.MaterialId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                        ImportedQuantity = dto.QuantityChange,
                        RemainingQuantity = dto.QuantityChange,
                        UnitCost = estimatedUnitCost,
                        TotalCost = dto.QuantityChange * estimatedUnitCost,
                        Status = "Active",
                        ImportedAt = DateTime.UtcNow,
                        SupplierName = "Inventory adjustment"
                    };
                    _context.MaterialBatches.Add(batch);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    batch.ImportedQuantity += dto.QuantityChange;
                    batch.RemainingQuantity += dto.QuantityChange;
                    batch.TotalCost = batch.ImportedQuantity * batch.UnitCost;
                    if (batch.Status != "Active") batch.Status = "Active";
                }
            }
            else if (batch != null)
            {
                var decrease = Math.Abs(dto.QuantityChange);
                if (batch.RemainingQuantity < decrease)
                {
                    throw new BadRequestException("Batch remaining quantity is lower than adjustment decrease.");
                }
                batch.RemainingQuantity -= decrease;
                if (batch.RemainingQuantity == 0) batch.Status = "Depleted";
                adjustmentUnitCost = batch.UnitCost;
            }
            else
            {
                var decrease = Math.Abs(dto.QuantityChange);
                var remainingDecrease = decrease;
                decimal costAmount = 0;
                decimal adjustedFromBatches = 0;

                var batches = await _context.MaterialBatches
                    .Where(b => b.WarehouseId == warehouse.WarehouseId
                        && b.MaterialId == material.MaterialId
                        && b.RemainingQuantity > 0
                        && b.Status == "Active")
                    .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.ImportedAt)
                    .ToListAsync();

                foreach (var fifoBatch in batches)
                {
                    if (remainingDecrease <= 0) break;

                    var used = Math.Min(fifoBatch.RemainingQuantity, remainingDecrease);
                    fifoBatch.RemainingQuantity -= used;
                    if (fifoBatch.RemainingQuantity == 0) fifoBatch.Status = "Depleted";

                    costAmount += used * fifoBatch.UnitCost;
                    adjustedFromBatches += used;
                    remainingDecrease -= used;
                }

                if (remainingDecrease > 0 && !branch.AllowNegativeStock)
                {
                    throw new BadRequestException("Batch remaining quantity is lower than adjustment decrease.");
                }

                adjustmentUnitCost = adjustedFromBatches > 0
                    ? costAmount / adjustedFromBatches
                    : await EstimateUnitCostAsync(material.MaterialId);
            }

            stock.CurrentQuantity = after;
            stock.UpdatedAt = DateTime.UtcNow;
            var unitCost = adjustmentUnitCost ?? batch?.UnitCost ?? await EstimateUnitCostAsync(material.MaterialId);

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                WarehouseId = warehouse.WarehouseId,
                BranchId = branchId,
                MaterialId = material.MaterialId,
                MaterialBatchId = batch?.MaterialBatchId,
                TransactionType = "Adjustment",
                Quantity = dto.QuantityChange,
                UnitCost = unitCost,
                CostAmount = dto.QuantityChange * unitCost,
                BeforeQuantity = before,
                AfterQuantity = after,
                CreatedByUserId = managerUserId,
                Note = dto.Reason
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return await GetStockDtoAsync(stock.WarehouseId, stock.MaterialId);
        }

        public async Task<List<InventoryTransactionDTO>> GetManagerTransactionsAsync(int managerUserId, int? materialId = null, DateTime? from = null, DateTime? to = null, string? type = null)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var transactions = await QueryTransactions()
                .Where(t => t.BranchId == branchId || t.Warehouse.BranchId == branchId)
                .Where(t => materialId == null || t.MaterialId == materialId.Value)
                .Where(t => from == null || t.CreatedAt >= from.Value)
                .Where(t => to == null || t.CreatedAt <= to.Value)
                .Where(t => string.IsNullOrWhiteSpace(type) || t.TransactionType == type)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return transactions.Select(MapTransaction).ToList();
        }

        public async Task<BranchInventorySettingDTO> GetBranchInventorySettingAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId) ?? throw new NotFoundException("Branch not found.");
            return MapBranchInventorySetting(branch);
        }

        public async Task<BranchInventorySettingDTO> UpdateBranchInventorySettingAsync(int branchId, UpdateBranchInventorySettingDTO dto)
        {
            var branch = await _context.Branches.FindAsync(branchId) ?? throw new NotFoundException("Branch not found.");
            branch.AllowNegativeStock = dto.AllowNegativeStock;
            branch.NegativeStockLimit = dto.NegativeStockLimit;
            await _context.SaveChangesAsync();
            return MapBranchInventorySetting(branch);
        }

        private async Task<Material> ValidateMaterialForImportAsync(ImportMaterialBatchDTO dto)
        {
            var material = await _context.Materials.FindAsync(dto.MaterialId) ?? throw new NotFoundException("Material not found.");
            var manufactureDate = NormalizeDate(dto.ManufactureDate);
            var expiryDate = NormalizeDate(dto.ExpiryDate);
            if (!material.IsActive) throw new BadRequestException("Cannot import inactive material.");
            if (material.RequiresExpiryTracking && expiryDate == null) throw new BadRequestException("Expiry date is required for this material.");
            if (expiryDate.HasValue && expiryDate.Value <= DateTime.UtcNow.Date) throw new BadRequestException("Expiry date must be in the future.");
            if (manufactureDate.HasValue && expiryDate.HasValue && manufactureDate.Value > expiryDate.Value)
            {
                throw new BadRequestException("Manufacture date cannot be after expiry date.");
            }
            return material;
        }

        private async Task<int> GetManagerBranchIdAsync(int managerUserId)
        {
            var branchId = await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == managerUserId)
                .Select(e => e.BranchId)
                .FirstOrDefaultAsync();
            if (!branchId.HasValue) throw new BadRequestException("Manager is not assigned to a branch.");
            return branchId.Value;
        }

        private async Task<Warehouse> GetOrCreateBranchWarehouseAsync(int branchId)
        {
            var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Type == "Branch" && w.BranchId == branchId);
            if (warehouse != null) return warehouse;
            var branch = await _context.Branches.FindAsync(branchId) ?? throw new NotFoundException("Branch not found.");
            warehouse = new Warehouse { Name = $"Kho {branch.Name}", Type = "Branch", BranchId = branchId, IsActive = true };
            _context.Warehouses.Add(warehouse);
            await _context.SaveChangesAsync();
            return warehouse;
        }

        private async Task<WarehouseStock> GetOrCreateStockAsync(int warehouseId, int materialId, decimal minStockLevel)
        {
            var stock = await _context.WarehouseStocks.FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.MaterialId == materialId);
            if (stock != null) return stock;
            stock = new WarehouseStock { WarehouseId = warehouseId, MaterialId = materialId, CurrentQuantity = 0, MinStockLevel = minStockLevel, UpdatedAt = DateTime.UtcNow };
            _context.WarehouseStocks.Add(stock);
            await _context.SaveChangesAsync();
            return stock;
        }

        private async Task<decimal> EstimateUnitCostAsync(int materialId)
        {
            return await _context.MaterialBatches
                .Where(b => b.MaterialId == materialId)
                .OrderByDescending(b => b.ImportedAt)
                .Select(b => (decimal?)b.UnitCost)
                .FirstOrDefaultAsync() ?? 0;
        }

        private IQueryable<WarehouseStock> QueryStocks()
        {
            return _context.WarehouseStocks.Include(s => s.Warehouse).ThenInclude(w => w.Branch).Include(s => s.Material);
        }

        private IQueryable<MaterialBatch> QueryBatches()
        {
            return _context.MaterialBatches.Include(b => b.Material).Include(b => b.Warehouse);
        }

        private IQueryable<InventoryTransaction> QueryTransactions()
        {
            return _context.InventoryTransactions.Include(t => t.Warehouse).ThenInclude(w => w.Branch).Include(t => t.Material).Include(t => t.MaterialBatch).Include(t => t.Branch);
        }

        private async Task<MaterialBatchDTO> GetBatchDtoAsync(int batchId)
        {
            var batch = await QueryBatches().FirstAsync(b => b.MaterialBatchId == batchId);
            return MapBatch(batch);
        }

        private async Task<WarehouseStockDTO> GetStockDtoAsync(int warehouseId, int materialId)
        {
            var stock = await QueryStocks().FirstAsync(s => s.WarehouseId == warehouseId && s.MaterialId == materialId);
            return MapStock(stock);
        }

        private static WarehouseStockDTO MapStock(WarehouseStock stock)
        {
            return new WarehouseStockDTO
            {
                WarehouseId = stock.WarehouseId,
                WarehouseName = stock.Warehouse.Name,
                WarehouseType = stock.Warehouse.Type,
                BranchId = stock.Warehouse.BranchId,
                BranchName = stock.Warehouse.Branch != null ? stock.Warehouse.Branch.Name : null,
                MaterialId = stock.MaterialId,
                MaterialName = stock.Material.Name,
                Unit = stock.Material.Unit,
                CurrentQuantity = stock.CurrentQuantity,
                MinStockLevel = stock.MinStockLevel ?? stock.Material.DefaultMinStockLevel,
                IsLowStock = stock.CurrentQuantity <= (stock.MinStockLevel ?? stock.Material.DefaultMinStockLevel),
                UpdatedAt = stock.UpdatedAt
            };
        }

        private static MaterialBatchDTO MapBatch(MaterialBatch batch)
        {
            return new MaterialBatchDTO
            {
                MaterialBatchId = batch.MaterialBatchId,
                MaterialId = batch.MaterialId,
                MaterialName = batch.Material.Name,
                WarehouseId = batch.WarehouseId,
                WarehouseName = batch.Warehouse.Name,
                BatchCode = batch.BatchCode,
                ImportedQuantity = batch.ImportedQuantity,
                RemainingQuantity = batch.RemainingQuantity,
                UnitCost = batch.UnitCost,
                TotalCost = batch.TotalCost,
                ManufactureDate = batch.ManufactureDate.HasValue ? DateOnly.FromDateTime(batch.ManufactureDate.Value) : null,
                ExpiryDate = batch.ExpiryDate.HasValue ? DateOnly.FromDateTime(batch.ExpiryDate.Value) : null,
                SupplierName = batch.SupplierName,
                Status = batch.Status,
                ImportedAt = batch.ImportedAt
            };
        }

        private static InventoryTransactionDTO MapTransaction(InventoryTransaction transaction)
        {
            return new InventoryTransactionDTO
            {
                InventoryTransactionId = transaction.InventoryTransactionId,
                WarehouseId = transaction.WarehouseId,
                WarehouseName = transaction.Warehouse.Name,
                BranchId = transaction.BranchId ?? transaction.Warehouse.BranchId,
                BranchName = transaction.Branch != null ? transaction.Branch.Name : transaction.Warehouse.Branch != null ? transaction.Warehouse.Branch.Name : null,
                MaterialId = transaction.MaterialId,
                MaterialName = transaction.Material.Name,
                Unit = transaction.Material.Unit,
                MaterialBatchId = transaction.MaterialBatchId,
                BatchCode = transaction.MaterialBatch != null ? transaction.MaterialBatch.BatchCode : null,
                BookingId = transaction.BookingId,
                TransactionType = transaction.TransactionType,
                Quantity = transaction.Quantity,
                UnitCost = transaction.UnitCost,
                CostAmount = transaction.CostAmount,
                BeforeQuantity = transaction.BeforeQuantity,
                AfterQuantity = transaction.AfterQuantity,
                Note = transaction.Note,
                CreatedByUserId = transaction.CreatedByUserId,
                CreatedAt = transaction.CreatedAt
            };
        }

        private static BranchInventorySettingDTO MapBranchInventorySetting(Branch branch)
        {
            return new BranchInventorySettingDTO { BranchId = branch.BranchId, BranchName = branch.Name, AllowNegativeStock = branch.AllowNegativeStock, NegativeStockLimit = branch.NegativeStockLimit };
        }

        private static DateTime? NormalizeDate(DateTime? value)
        {
            return value?.Date;
        }
    }
}
