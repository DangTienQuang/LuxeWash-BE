using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;

namespace AutoWashPro.BLL.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly AutoWashDbContext _context;
        private static readonly Dictionary<string, string> UnitAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ml"] = "milliliter",
            ["milliliter"] = "milliliter",
            ["milliliters"] = "milliliter",
            ["millilitre"] = "milliliter",
            ["millilitres"] = "milliliter",
            ["l"] = "liter",
            ["liter"] = "liter",
            ["liters"] = "liter",
            ["litre"] = "liter",
            ["litres"] = "liter",
            ["g"] = "gram",
            ["gram"] = "gram",
            ["grams"] = "gram",
            ["kg"] = "kilogram",
            ["kilogram"] = "kilogram",
            ["kilograms"] = "kilogram",
            ["piece"] = "piece",
            ["pieces"] = "piece",
            ["pcs"] = "piece",
            ["pc"] = "piece",
            ["cai"] = "piece"
        };

        private static readonly Regex UnitCodePattern = new("^[a-z0-9_\\-]+$", RegexOptions.Compiled);

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

        public async Task<List<MaterialUnitDTO>> GetMaterialUnitsAsync(bool includeInactive = false)
        {
            return await _context.MaterialUnits
                .Where(u => includeInactive || u.IsActive)
                .OrderBy(u => u.MeasurementType)
                .ThenBy(u => u.DisplayName)
                .Select(u => MapMaterialUnit(u))
                .ToListAsync();
        }

        public async Task<MaterialUnitDTO> CreateMaterialUnitAsync(CreateMaterialUnitDTO dto)
        {
            var code = NormalizeUnitCode(dto.Code);
            if (await _context.MaterialUnits.AnyAsync(u => u.Code == code))
            {
                throw new BadRequestException($"Material unit code '{code}' already exists.");
            }

            var unit = new MaterialUnit
            {
                Code = code,
                DisplayName = dto.DisplayName.Trim(),
                MeasurementType = dto.MeasurementType.Trim(),
                IsActive = true
            };

            _context.MaterialUnits.Add(unit);
            await _context.SaveChangesAsync();
            return MapMaterialUnit(unit);
        }

        public async Task<MaterialUnitDTO> UpdateMaterialUnitAsync(int unitId, UpdateMaterialUnitDTO dto)
        {
            var unit = await _context.MaterialUnits.FindAsync(unitId)
                ?? throw new NotFoundException("Material unit not found.");

            if (!dto.IsActive && unit.IsActive && await _context.Materials.AnyAsync(m => m.Unit == unit.Code))
            {
                throw new BadRequestException("Cannot deactivate this material unit because it is being used by one or more materials.");
            }

            unit.DisplayName = dto.DisplayName.Trim();
            unit.MeasurementType = dto.MeasurementType.Trim();
            unit.IsActive = dto.IsActive;
            unit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapMaterialUnit(unit);
        }

        public async Task<MaterialDTO> CreateMaterialAsync(CreateMaterialDTO dto)
        {
            var normalizedUnit = await NormalizeUnitAsync(dto.Unit);
            var material = new Material
            {
                Name = dto.Name.Trim(),
                Category = dto.Category.Trim(),
                Unit = normalizedUnit,
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

            var normalizedUnit = NormalizeUnitInput(dto.Unit);
            if (!string.Equals(material.Unit, normalizedUnit, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureActiveUnitExistsAsync(dto.Unit);
                await EnsureUnitCanBeChangedAsync(materialId);
            }
            else
            {
                normalizedUnit = material.Unit;
            }

            material.Name = dto.Name.Trim();
            material.Category = dto.Category.Trim();
            material.Unit = normalizedUnit;
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
                    ManufactureDate = b.ManufactureDate.HasValue ? DateOnly.FromDateTime(b.ManufactureDate.Value) : null,
                    ExpiryDate = b.ExpiryDate.HasValue ? DateOnly.FromDateTime(b.ExpiryDate.Value) : null,
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

        private static MaterialUnitDTO MapMaterialUnit(MaterialUnit unit)
        {
            return new MaterialUnitDTO
            {
                UnitId = unit.UnitId,
                Code = unit.Code,
                DisplayName = unit.DisplayName,
                MeasurementType = unit.MeasurementType,
                IsActive = unit.IsActive
            };
        }

        private async Task EnsureUnitCanBeChangedAsync(int materialId)
        {
            var hasBatches = await _context.MaterialBatches.AnyAsync(b => b.MaterialId == materialId);
            var hasStocks = await _context.WarehouseStocks.AnyAsync(s => s.MaterialId == materialId);
            var hasServiceUsages = await _context.ServiceMaterialUsages.AnyAsync(u => u.MaterialId == materialId);
            var hasTransactions = await _context.InventoryTransactions.AnyAsync(t => t.MaterialId == materialId);
            var hasBookingUsages = await _context.BookingMaterialUsages.AnyAsync(u => u.MaterialId == materialId);
            var hasExtraUsageRequests = await _context.ExtraMaterialUsageRequests.AnyAsync(r => r.MaterialId == materialId);

            if (hasBatches || hasStocks || hasServiceUsages || hasTransactions || hasBookingUsages || hasExtraUsageRequests)
            {
                throw new BadRequestException("Cannot change material unit because this material already has inventory, usage, or transaction history.");
            }
        }

        private async Task<string> NormalizeUnitAsync(string unit)
        {
            var value = NormalizeUnitInput(unit);
            await EnsureActiveUnitExistsAsync(value);
            return value;
        }

        private string NormalizeUnitInput(string unit)
        {
            var value = NormalizeUnitCode(unit);
            if (UnitAliases.TryGetValue(value, out var normalizedUnit))
            {
                value = normalizedUnit;
            }

            return value;
        }

        private async Task EnsureActiveUnitExistsAsync(string unit)
        {
            var value = NormalizeUnitInput(unit);
            if (await _context.MaterialUnits.AnyAsync(u => u.Code == value && u.IsActive))
            {
                return;
            }

            var allowedUnits = await _context.MaterialUnits
                .Where(u => u.IsActive)
                .OrderBy(u => u.Code)
                .Select(u => u.Code)
                .ToListAsync();

            throw new BadRequestException($"Invalid material unit '{unit}'. Please select one active unit code. Allowed units: {string.Join(", ", allowedUnits)}.");
        }

        private static string NormalizeUnitCode(string unit)
        {
            var value = unit.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new BadRequestException("Material unit code is required.");
            }

            if (!UnitCodePattern.IsMatch(value))
            {
                throw new BadRequestException("Material unit code can only contain lowercase letters, numbers, hyphen, or underscore.");
            }

            return value;
        }

    }
}
