using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AutoWashPro.BLL.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly AutoWashDbContext _context;

        public MaterialService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<MaterialDTO>> GetMaterialsAsync(bool includeInactive = false)
        {
            return await _context.Materials
                .Where(m => includeInactive || m.IsActive)
                .OrderBy(m => m.Name)
                .Select(m => MapMaterial(m))
                .ToListAsync();
        }

        public async Task<MaterialDTO> CreateMaterialAsync(CreateMaterialDTO dto)
        {
            var material = new Material
            {
                Name = dto.Name.Trim(),
                Category = dto.Category.Trim(),
                Unit = dto.Unit.Trim(),
                Description = dto.Description,
                RequiresExpiryTracking = dto.RequiresExpiryTracking,
                DefaultMinStockLevel = dto.DefaultMinStockLevel,
                ExpiryWarningDays = dto.ExpiryWarningDays,
                IsActive = true
            };

            _context.Materials.Add(material);
            await _context.SaveChangesAsync();
            return MapMaterial(material);
        }

        public async Task<MaterialDTO> UpdateMaterialAsync(int materialId, UpdateMaterialDTO dto)
        {
            var material = await _context.Materials.FindAsync(materialId)
                ?? throw new NotFoundException("Material not found.");

            material.Name = dto.Name.Trim();
            material.Category = dto.Category.Trim();
            material.Unit = dto.Unit.Trim();
            material.Description = dto.Description;
            material.RequiresExpiryTracking = dto.RequiresExpiryTracking;
            material.DefaultMinStockLevel = dto.DefaultMinStockLevel;
            material.ExpiryWarningDays = dto.ExpiryWarningDays;
            material.IsActive = dto.IsActive;
            material.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapMaterial(material);
        }

        public async Task<List<WarehouseStockDTO>> GetStocksAsync(int? branchId = null)
        {
            return await _context.WarehouseStocks
                .Include(s => s.Warehouse).ThenInclude(w => w.Branch)
                .Include(s => s.Material)
                .Where(s => branchId == null || s.Warehouse.BranchId == branchId)
                .OrderBy(s => s.Warehouse.Type)
                .ThenBy(s => s.Warehouse.Name)
                .ThenBy(s => s.Material.Name)
                .Select(s => new WarehouseStockDTO
                {
                    WarehouseId = s.WarehouseId,
                    WarehouseName = s.Warehouse.Name,
                    WarehouseType = s.Warehouse.Type,
                    BranchId = s.Warehouse.BranchId,
                    BranchName = s.Warehouse.Branch != null ? s.Warehouse.Branch.Name : null,
                    MaterialId = s.MaterialId,
                    MaterialName = s.Material.Name,
                    Unit = s.Material.Unit,
                    CurrentQuantity = s.CurrentQuantity,
                    MinStockLevel = s.MinStockLevel ?? s.Material.DefaultMinStockLevel,
                    IsLowStock = s.CurrentQuantity <= (s.MinStockLevel ?? s.Material.DefaultMinStockLevel),
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<MaterialBatchDTO>> GetBatchesAsync(int? branchId = null, bool expiringOnly = false)
        {
            var today = DateTime.UtcNow.Date;
            return await _context.MaterialBatches
                .Include(b => b.Material)
                .Include(b => b.Warehouse)
                .Where(b => branchId == null || b.Warehouse.BranchId == branchId)
                .Where(b => !expiringOnly || (b.ExpiryDate != null
                    && b.RemainingQuantity > 0
                    && b.ExpiryDate.Value.Date <= today.AddDays(b.Material.ExpiryWarningDays)))
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.Material.Name)
                .Select(b => new MaterialBatchDTO
                {
                    MaterialBatchId = b.MaterialBatchId,
                    MaterialId = b.MaterialId,
                    MaterialName = b.Material.Name,
                    WarehouseId = b.WarehouseId,
                    WarehouseName = b.Warehouse.Name,
                    BatchCode = b.BatchCode,
                    ImportedQuantity = b.ImportedQuantity,
                    RemainingQuantity = b.RemainingQuantity,
                    UnitCost = b.UnitCost,
                    TotalCost = b.TotalCost,
                    ExpiryDate = b.ExpiryDate,
                    SupplierName = b.SupplierName,
                    Status = b.Status,
                    ImportedAt = b.ImportedAt
                })
                .ToListAsync();
        }

        public async Task DiscardBatchAsync(int batchId, string? note = null)
        {
            var batch = await _context.MaterialBatches
                .Include(b => b.Warehouse)
                .FirstOrDefaultAsync(b => b.MaterialBatchId == batchId)
                ?? throw new NotFoundException("Batch not found.");

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
                throw new BadRequestException("Warehouse stock is lower than the batch remaining quantity. Please reconcile stock before discarding.");
            }

            stock.CurrentQuantity -= discardQuantity;
            stock.UpdatedAt = DateTime.UtcNow;
            batch.RemainingQuantity = 0;
            batch.Status = "Discarded";

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                WarehouseId = batch.WarehouseId,
                BranchId = batch.Warehouse.BranchId,
                MaterialId = batch.MaterialId,
                MaterialBatchId = batch.MaterialBatchId,
                TransactionType = "Discard",
                Quantity = discardQuantity,
                UnitCost = batch.UnitCost,
                CostAmount = discardQuantity * batch.UnitCost,
                BeforeQuantity = before,
                AfterQuantity = stock.CurrentQuantity,
                Note = note ?? "Discarded material batch"
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        private static MaterialDTO MapMaterial(Material material)
        {
            return new MaterialDTO
            {
                MaterialId = material.MaterialId,
                Name = material.Name,
                Category = material.Category,
                Unit = material.Unit,
                Description = material.Description,
                RequiresExpiryTracking = material.RequiresExpiryTracking,
                DefaultMinStockLevel = material.DefaultMinStockLevel,
                ExpiryWarningDays = material.ExpiryWarningDays,
                IsActive = material.IsActive
            };
        }

    }
}
