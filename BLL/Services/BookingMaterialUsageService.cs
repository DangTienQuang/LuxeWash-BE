using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AutoWashPro.BLL.Services
{
    public class BookingMaterialUsageService : IBookingMaterialUsageService
    {
        private readonly AutoWashDbContext _context;

        public BookingMaterialUsageService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task ConsumeForCompletedBookingAsync(int bookingId, int? actorUserId = null)
        {
            if (await _context.BookingMaterialUsages.AnyAsync(u => u.BookingId == bookingId && u.UsageType == "Standard"))
            {
                return;
            }

            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .Include(b => b.Vehicle)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.Status != "Completed")
            {
                throw new BadRequestException("Materials can only be consumed after booking is completed.");
            }

            var vehicleTypeId = booking.ActualVehicleTypeId ?? booking.Vehicle?.VehicleTypeId;
            if (!vehicleTypeId.HasValue)
            {
                throw new BadRequestException("Cannot resolve vehicle type for material usage.");
            }

            var multiplier = await GetConditionMultiplierAsync(booking.VehicleCondition);
            var weightMultiplier = await GetVehicleWeightMultiplierAsync(vehicleTypeId.Value);
            var plannedUsages = new List<(int BookingDetailId, int MaterialId, decimal Quantity, int ServiceId)>();

            foreach (var detail in booking.BookingDetails)
            {
                var usages = await GetMaterialUsagesForDetailAsync(detail.ServiceId, vehicleTypeId.Value);
                plannedUsages.AddRange(usages.Select(usage => (
                    detail.DetailId,
                    usage.MaterialId,
                    decimal.Round(usage.BaseQuantity * multiplier * weightMultiplier, 4),
                    detail.ServiceId)));
            }

            if (plannedUsages.Count == 0)
            {
                return;
            }

            var branchWarehouse = await GetBranchWarehouseAsync(booking.BranchId);

            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            foreach (var usage in plannedUsages)
            {
                await ConsumeMaterialAsync(branchWarehouse, booking, usage.BookingDetailId, usage.MaterialId, usage.Quantity, "Standard", actorUserId,
                    $"Booking #{booking.BookingId} service #{usage.ServiceId}");
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task<ExtraMaterialUsageRequestDTO> CreateExtraUsageRequestAsync(int bookingId, int actorUserId, ReportExtraMaterialUsageDTO dto)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.ProcessingStaffId != actorUserId)
            {
                throw new ForbiddenException("You are not assigned to this booking.");
            }

            if (booking.Status != "Processing" && booking.Status != "Completed")
            {
                throw new BadRequestException("Extra material usage can only be reported for processing or completed bookings.");
            }

            var material = await _context.Materials.FindAsync(dto.MaterialId)
                ?? throw new NotFoundException("Material not found.");
            EnsureActiveMaterial(material);

            var request = new ExtraMaterialUsageRequest
            {
                BookingId = booking.BookingId,
                StaffUserId = actorUserId,
                BranchId = booking.BranchId,
                MaterialId = dto.MaterialId,
                Quantity = dto.Quantity,
                Reason = dto.Note,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.ExtraMaterialUsageRequests.Add(request);
            await _context.SaveChangesAsync();
            return await GetExtraUsageRequestDtoAsync(request.RequestId);
        }

        public async Task<List<ExtraMaterialUsageRequestDTO>> GetManagerExtraUsageRequestsAsync(int managerUserId, string? status = null)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var requests = await QueryExtraUsageRequests()
                .Where(r => r.BranchId == branchId && (string.IsNullOrWhiteSpace(status) || r.Status == status))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return requests.Select(MapExtraUsageRequest).ToList();
        }

        public async Task<ExtraMaterialUsageRequestDTO> ApproveExtraUsageRequestAsync(int managerUserId, int requestId, ReviewExtraMaterialUsageRequestDTO dto)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var request = await QueryExtraUsageRequests()
                .FirstOrDefaultAsync(r => r.RequestId == requestId)
                ?? throw new NotFoundException("Extra material usage request not found.");

            if (request.BranchId != branchId)
            {
                throw new ForbiddenException("This request does not belong to your branch.");
            }

            if (request.Status != "Pending")
            {
                throw new BadRequestException("Only pending extra usage requests can be approved.");
            }

            var branchWarehouse = await GetBranchWarehouseAsync(request.BranchId);
            using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await ConsumeMaterialAsync(branchWarehouse, request.Booking, null, request.MaterialId, request.Quantity, "Extra", managerUserId, request.Reason);

            request.Status = "Approved";
            request.ReviewedByManagerId = managerUserId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ManagerNote = dto.ManagerNote;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return await GetExtraUsageRequestDtoAsync(request.RequestId);
        }

        public async Task<ExtraMaterialUsageRequestDTO> RejectExtraUsageRequestAsync(int managerUserId, int requestId, ReviewExtraMaterialUsageRequestDTO dto)
        {
            var branchId = await GetManagerBranchIdAsync(managerUserId);
            var request = await _context.ExtraMaterialUsageRequests.FirstOrDefaultAsync(r => r.RequestId == requestId)
                ?? throw new NotFoundException("Extra material usage request not found.");

            if (request.BranchId != branchId)
            {
                throw new ForbiddenException("This request does not belong to your branch.");
            }

            if (request.Status != "Pending")
            {
                throw new BadRequestException("Only pending extra usage requests can be rejected.");
            }

            request.Status = "Rejected";
            request.ReviewedByManagerId = managerUserId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ManagerNote = dto.ManagerNote;

            await _context.SaveChangesAsync();
            return await GetExtraUsageRequestDtoAsync(request.RequestId);
        }

        private async Task<List<ServiceMaterialUsage>> GetMaterialUsagesForDetailAsync(int serviceId, int vehicleTypeId)
        {
            var vehicleSpecific = await _context.ServiceMaterialUsages
                .Where(u => u.ServiceId == serviceId
                    && u.VehicleTypeId == vehicleTypeId
                    && u.IsActive
                    && u.Material.IsActive)
                .ToListAsync();

            var defaultUsages = await _context.ServiceMaterialUsages
                .Where(u => u.ServiceId == serviceId
                    && u.VehicleTypeId == null
                    && u.IsActive
                    && u.Material.IsActive)
                .ToListAsync();

            var materialIdsWithVehicleSpecific = vehicleSpecific.Select(u => u.MaterialId).ToHashSet();
            return vehicleSpecific
                .Concat(defaultUsages.Where(u => !materialIdsWithVehicleSpecific.Contains(u.MaterialId)))
                .ToList();
        }

        private async Task ConsumeMaterialAsync(Warehouse warehouse, Booking booking, int? bookingDetailId, int materialId, decimal quantity, string usageType, int? actorUserId, string? note)
        {
            if (quantity <= 0) return;

            var material = await _context.Materials.FindAsync(materialId)
                ?? throw new NotFoundException("Material not found.");
            EnsureActiveMaterial(material);
            var branch = warehouse.Branch
                ?? throw new BadRequestException("Branch warehouse is not linked to a branch.");

            var batches = await _context.MaterialBatches
                .Where(b => b.WarehouseId == warehouse.WarehouseId
                    && b.MaterialId == materialId
                    && b.RemainingQuantity > 0
                    && b.Status == "Active"
                    && (b.ExpiryDate == null || b.ExpiryDate.Value.Date > DateTime.UtcNow.Date))
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.ImportedAt)
                .ToListAsync();

            var availableQuantity = batches.Sum(b => b.RemainingQuantity);

            var stock = await _context.WarehouseStocks
                .FirstOrDefaultAsync(s => s.WarehouseId == warehouse.WarehouseId && s.MaterialId == materialId);

            if (stock == null)
            {
                if (!branch.AllowNegativeStock)
                {
                    throw new BadRequestException("Branch warehouse stock not found.");
                }

                stock = new WarehouseStock
                {
                    WarehouseId = warehouse.WarehouseId,
                    MaterialId = materialId,
                    CurrentQuantity = 0,
                    MinStockLevel = material.DefaultMinStockLevel,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WarehouseStocks.Add(stock);
                await _context.SaveChangesAsync();
            }

            if (availableQuantity < quantity && !branch.AllowNegativeStock)
            {
                throw new BadRequestException($"Branch warehouse does not have enough {material.Name}. Available: {availableQuantity} {material.Unit}; required: {quantity} {material.Unit}.");
            }

            var projectedAfterQuantity = stock.CurrentQuantity - quantity;
            if (branch.AllowNegativeStock
                && branch.NegativeStockLimit.HasValue
                && projectedAfterQuantity < -branch.NegativeStockLimit.Value)
            {
                throw new BadRequestException($"Negative stock limit exceeded for {material.Name}. Limit: -{branch.NegativeStockLimit.Value} {material.Unit}; projected: {projectedAfterQuantity} {material.Unit}.");
            }

            var remaining = quantity;
            foreach (var batch in batches)
            {
                if (remaining <= 0) break;

                var used = Math.Min(batch.RemainingQuantity, remaining);
                var before = stock.CurrentQuantity;
                batch.RemainingQuantity -= used;
                if (batch.RemainingQuantity == 0) batch.Status = "Depleted";

                stock.CurrentQuantity -= used;
                stock.UpdatedAt = DateTime.UtcNow;

                var cost = used * batch.UnitCost;
                _context.BookingMaterialUsages.Add(new BookingMaterialUsage
                {
                    BookingId = booking.BookingId,
                    BookingDetailId = bookingDetailId,
                    BranchId = booking.BranchId,
                    MaterialId = materialId,
                    MaterialBatchId = batch.MaterialBatchId,
                    QuantityUsed = used,
                    UnitCost = batch.UnitCost,
                    CostAmount = cost,
                    IsCostPending = false,
                    UsageType = usageType,
                    CreatedAt = DateTime.UtcNow
                });

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    WarehouseId = warehouse.WarehouseId,
                    BranchId = booking.BranchId,
                    MaterialId = materialId,
                    MaterialBatchId = batch.MaterialBatchId,
                    BookingId = booking.BookingId,
                    TransactionType = usageType == "Extra" ? "ExtraUsage" : "Usage",
                    Quantity = used,
                    UnitCost = batch.UnitCost,
                    CostAmount = cost,
                    BeforeQuantity = before,
                    AfterQuantity = stock.CurrentQuantity,
                    CreatedByUserId = actorUserId,
                    Note = note
                });

                remaining -= used;
            }

            if (remaining > 0)
            {
                var estimatedUnitCost = await EstimateUnitCostAsync(materialId);
                var before = stock.CurrentQuantity;
                stock.CurrentQuantity -= remaining;
                stock.UpdatedAt = DateTime.UtcNow;
                var cost = remaining * estimatedUnitCost;

                _context.BookingMaterialUsages.Add(new BookingMaterialUsage
                {
                    BookingId = booking.BookingId,
                    BookingDetailId = bookingDetailId,
                    BranchId = booking.BranchId,
                    MaterialId = materialId,
                    MaterialBatchId = null,
                    QuantityUsed = remaining,
                    UnitCost = estimatedUnitCost,
                    EstimatedUnitCost = estimatedUnitCost,
                    CostAmount = cost,
                    IsCostPending = true,
                    UsageType = usageType,
                    CreatedAt = DateTime.UtcNow
                });

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    WarehouseId = warehouse.WarehouseId,
                    BranchId = booking.BranchId,
                    MaterialId = materialId,
                    MaterialBatchId = null,
                    BookingId = booking.BookingId,
                    TransactionType = usageType == "Extra" ? "ExtraUsage" : "Usage",
                    Quantity = remaining,
                    UnitCost = estimatedUnitCost,
                    CostAmount = cost,
                    BeforeQuantity = before,
                    AfterQuantity = stock.CurrentQuantity,
                    CreatedByUserId = actorUserId,
                    Note = $"Negative stock usage. {note}".Trim()
                });
            }
        }

        private async Task<Warehouse> GetBranchWarehouseAsync(int branchId)
        {
            var warehouse = await _context.Warehouses
                .Include(w => w.Branch)
                .FirstOrDefaultAsync(w => w.Type == "Branch" && w.BranchId == branchId);

            if (warehouse != null)
            {
                return warehouse;
            }

            var branch = await _context.Branches.FindAsync(branchId)
                ?? throw new NotFoundException("Branch not found.");

            if (!branch.AllowNegativeStock)
            {
                throw new BadRequestException("Branch warehouse has not received inventory yet.");
            }

            warehouse = new Warehouse
            {
                Name = $"Kho {branch.Name}",
                Type = "Branch",
                BranchId = branchId,
                Branch = branch,
                IsActive = true
            };
            _context.Warehouses.Add(warehouse);
            await _context.SaveChangesAsync();
            return warehouse;
        }

        private async Task<decimal> GetConditionMultiplierAsync(VehicleCondition condition)
        {
            var multiplier = await _context.VehicleConditionMaterialMultipliers
                .Where(m => m.VehicleCondition == condition && m.IsActive)
                .Select(m => (decimal?)m.Multiplier)
                .FirstOrDefaultAsync();

            if (multiplier.HasValue) return multiplier.Value;

            return condition switch
            {
                VehicleCondition.Dirty => 1.5m,
                VehicleCondition.VeryDirty => 2.0m,
                _ => 1.0m
            };
        }

        private async Task<decimal> GetVehicleWeightMultiplierAsync(int vehicleTypeId)
        {
            var baseWeight = await _context.VehicleTypes
                .Where(v => v.Id == vehicleTypeId)
                .Select(v => (int?)v.BaseWeight)
                .FirstOrDefaultAsync();

            if (!baseWeight.HasValue || baseWeight.Value <= 0)
            {
                return 1m;
            }

            return Math.Max(1m, baseWeight.Value);
        }

        private async Task<decimal> EstimateUnitCostAsync(int materialId)
        {
            return await _context.MaterialBatches
                .Where(b => b.MaterialId == materialId)
                .OrderByDescending(b => b.ImportedAt)
                .Select(b => (decimal?)b.UnitCost)
                .FirstOrDefaultAsync() ?? 0;
        }

        private async Task<int> GetManagerBranchIdAsync(int managerUserId)
        {
            var branchId = await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == managerUserId)
                .Select(e => e.BranchId)
                .FirstOrDefaultAsync();

            if (!branchId.HasValue)
            {
                throw new BadRequestException("Manager is not assigned to a branch.");
            }

            return branchId.Value;
        }

        private IQueryable<ExtraMaterialUsageRequest> QueryExtraUsageRequests()
        {
            return _context.ExtraMaterialUsageRequests
                .Include(r => r.Material)
                .Include(r => r.Booking);
        }

        private async Task<ExtraMaterialUsageRequestDTO> GetExtraUsageRequestDtoAsync(int requestId)
        {
            var request = await QueryExtraUsageRequests()
                .FirstAsync(r => r.RequestId == requestId);

            return MapExtraUsageRequest(request);
        }

        private static ExtraMaterialUsageRequestDTO MapExtraUsageRequest(ExtraMaterialUsageRequest request)
        {
            return new ExtraMaterialUsageRequestDTO
            {
                RequestId = request.RequestId,
                BookingId = request.BookingId,
                StaffUserId = request.StaffUserId,
                BranchId = request.BranchId,
                MaterialId = request.MaterialId,
                MaterialName = request.Material.Name,
                Unit = request.Material.Unit,
                Quantity = request.Quantity,
                Reason = request.Reason,
                Status = request.Status,
                ReviewedByManagerId = request.ReviewedByManagerId,
                ReviewedAt = request.ReviewedAt,
                ManagerNote = request.ManagerNote,
                CreatedAt = request.CreatedAt
            };
        }

        private static void EnsureActiveMaterial(Material material)
        {
            if (!material.IsActive)
            {
                throw new BadRequestException("Material is inactive.");
            }
        }
    }
}
