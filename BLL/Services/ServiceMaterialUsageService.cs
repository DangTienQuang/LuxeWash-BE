using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class ServiceMaterialUsageService : IServiceMaterialUsageService
    {
        private readonly AutoWashDbContext _context;

        public ServiceMaterialUsageService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<ServiceMaterialUsageDTO>> GetByServiceAsync(int serviceId)
        {
            await EnsureServiceExistsAsync(serviceId);

            var usages = await _context.ServiceMaterialUsages
                .Include(u => u.Service)
                .Include(u => u.VehicleType)
                .Include(u => u.Material)
                .Where(u => u.ServiceId == serviceId)
                .OrderBy(u => u.VehicleTypeId)
                .ThenBy(u => u.Material.Name)
                .ToListAsync();

            return usages.Select(MapUsage).ToList();
        }

        public async Task<List<ServiceMaterialUsageDTO>> UpsertAsync(int serviceId, UpsertServiceMaterialUsageDTO dto)
        {
            await EnsureServiceExistsAsync(serviceId);
            var items = ResolveItems(dto);

            if (dto.VehicleTypeId.HasValue && !await _context.VehicleTypes.AnyAsync(v => v.Id == dto.VehicleTypeId.Value))
            {
                throw new NotFoundException("Vehicle type not found.");
            }

            var materialIds = items.Select(i => i.MaterialId).Distinct().ToList();
            if (materialIds.Count != items.Count)
            {
                throw new BadRequestException("Material list contains duplicate items.");
            }

            var materials = await _context.Materials
                .Where(m => materialIds.Contains(m.MaterialId))
                .ToDictionaryAsync(m => m.MaterialId);

            if (materials.Count != materialIds.Count)
            {
                throw new NotFoundException("One or more materials were not found.");
            }
            if (materials.Values.Any(m => !m.IsActive))
            {
                throw new BadRequestException("Cannot configure inactive material for a service.");
            }

            var existingUsages = await _context.ServiceMaterialUsages
                .Where(u => u.ServiceId == serviceId
                    && u.VehicleTypeId == dto.VehicleTypeId
                    && materialIds.Contains(u.MaterialId))
                .ToListAsync();

            foreach (var item in items)
            {
                var material = materials[item.MaterialId];
                var usage = existingUsages.FirstOrDefault(u => u.MaterialId == item.MaterialId);
                if (usage == null)
                {
                    usage = new ServiceMaterialUsage
                    {
                        ServiceId = serviceId,
                        VehicleTypeId = dto.VehicleTypeId,
                        MaterialId = item.MaterialId
                    };
                    _context.ServiceMaterialUsages.Add(usage);
                }

                usage.BaseQuantity = item.BaseQuantity;
                usage.Unit = material.Unit;
                usage.IsActive = true;
            }

            await _context.SaveChangesAsync();
            return await GetByServiceAsync(serviceId);
        }

        public async Task<ServiceMaterialUsageDTO> UpdateAsync(int serviceId, int usageId, UpsertServiceMaterialUsageDTO dto)
        {
            var usage = await _context.ServiceMaterialUsages.FirstOrDefaultAsync(u => u.ServiceMaterialUsageId == usageId && u.ServiceId == serviceId)
                ?? throw new NotFoundException("Service material usage not found.");

            if (!dto.MaterialId.HasValue || !dto.BaseQuantity.HasValue)
            {
                throw new BadRequestException("materialId and baseQuantity are required.");
            }
            if (dto.BaseQuantity.Value <= 0)
            {
                throw new BadRequestException("BaseQuantity must be greater than 0.");
            }

            var material = await _context.Materials.FindAsync(dto.MaterialId.Value)
                ?? throw new NotFoundException("Material not found.");
            if (!material.IsActive)
            {
                throw new BadRequestException("Cannot configure inactive material for a service.");
            }

            if (dto.VehicleTypeId.HasValue && !await _context.VehicleTypes.AnyAsync(v => v.Id == dto.VehicleTypeId.Value))
            {
                throw new NotFoundException("Vehicle type not found.");
            }

            var duplicate = await _context.ServiceMaterialUsages.AnyAsync(u =>
                u.ServiceMaterialUsageId != usageId
                && u.ServiceId == serviceId
                && u.VehicleTypeId == dto.VehicleTypeId
                && u.MaterialId == dto.MaterialId.Value);
            if (duplicate)
            {
                throw new BadRequestException("Material usage already exists for this service and vehicle type.");
            }

            usage.VehicleTypeId = dto.VehicleTypeId;
            usage.MaterialId = dto.MaterialId.Value;
            usage.BaseQuantity = dto.BaseQuantity.Value;
            usage.Unit = material.Unit;
            usage.IsActive = true;

            await _context.SaveChangesAsync();
            return await GetUsageDtoAsync(usage.ServiceMaterialUsageId);
        }

        public async Task<List<VehicleConditionMaterialMultiplierDTO>> GetConditionMultipliersAsync()
        {
            await EnsureDefaultMultipliersAsync();
            return await _context.VehicleConditionMaterialMultipliers
                .OrderBy(m => m.VehicleCondition)
                .Select(m => new VehicleConditionMaterialMultiplierDTO
                {
                    Id = m.Id,
                    VehicleCondition = m.VehicleCondition.ToString(),
                    Multiplier = m.Multiplier,
                    Description = m.Description,
                    IsActive = m.IsActive
                })
                .ToListAsync();
        }

        public async Task<VehicleConditionMaterialMultiplierDTO> UpdateConditionMultiplierAsync(int id, UpdateVehicleConditionMaterialMultiplierDTO dto)
        {
            var multiplier = await _context.VehicleConditionMaterialMultipliers.FindAsync(id)
                ?? throw new NotFoundException("Condition multiplier not found.");

            multiplier.Multiplier = dto.Multiplier;
            multiplier.Description = dto.Description;
            multiplier.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();

            return new VehicleConditionMaterialMultiplierDTO
            {
                Id = multiplier.Id,
                VehicleCondition = multiplier.VehicleCondition.ToString(),
                Multiplier = multiplier.Multiplier,
                Description = multiplier.Description,
                IsActive = multiplier.IsActive
            };
        }

        private async Task EnsureServiceExistsAsync(int serviceId)
        {
            if (!await _context.Services.AnyAsync(s => s.ServiceId == serviceId))
            {
                throw new NotFoundException("Service not found.");
            }
        }

        private async Task EnsureDefaultMultipliersAsync()
        {
            if (await _context.VehicleConditionMaterialMultipliers.AnyAsync()) return;

            _context.VehicleConditionMaterialMultipliers.AddRange(
                new VehicleConditionMaterialMultiplier { VehicleCondition = VehicleCondition.Clean, Multiplier = 1.0m, Description = "Standard material usage" },
                new VehicleConditionMaterialMultiplier { VehicleCondition = VehicleCondition.Dirty, Multiplier = 1.5m, Description = "Dirty vehicle material usage" },
                new VehicleConditionMaterialMultiplier { VehicleCondition = VehicleCondition.VeryDirty, Multiplier = 2.0m, Description = "Very dirty vehicle material usage" });

            await _context.SaveChangesAsync();
        }

        private static List<UpsertServiceMaterialUsageItemDTO> ResolveItems(UpsertServiceMaterialUsageDTO dto)
        {
            if (dto.Items != null && dto.Items.Count > 0)
            {
                return dto.Items;
            }

            if (!dto.MaterialId.HasValue || !dto.BaseQuantity.HasValue)
            {
                throw new BadRequestException("Please provide materialId/baseQuantity or at least one item.");
            }
            if (dto.BaseQuantity.Value <= 0)
            {
                throw new BadRequestException("BaseQuantity must be greater than 0.");
            }

            return new List<UpsertServiceMaterialUsageItemDTO>
            {
                new UpsertServiceMaterialUsageItemDTO
                {
                    MaterialId = dto.MaterialId.Value,
                    BaseQuantity = dto.BaseQuantity.Value
                }
            };
        }

        private async Task<ServiceMaterialUsageDTO> GetUsageDtoAsync(int usageId)
        {
            var usage = await _context.ServiceMaterialUsages
                .Include(u => u.Service)
                .Include(u => u.VehicleType)
                .Include(u => u.Material)
                .FirstAsync(u => u.ServiceMaterialUsageId == usageId);

            return MapUsage(usage);
        }

        private static ServiceMaterialUsageDTO MapUsage(ServiceMaterialUsage usage)
        {
            return new ServiceMaterialUsageDTO
            {
                ServiceMaterialUsageId = usage.ServiceMaterialUsageId,
                ServiceId = usage.ServiceId,
                ServiceName = usage.Service.ServiceName,
                VehicleTypeId = usage.VehicleTypeId,
                VehicleTypeName = usage.VehicleType != null ? usage.VehicleType.Name : null,
                MaterialId = usage.MaterialId,
                MaterialName = usage.Material.Name,
                BaseQuantity = usage.BaseQuantity,
                Unit = usage.Unit,
                IsActive = usage.IsActive
            };
        }
    }
}
