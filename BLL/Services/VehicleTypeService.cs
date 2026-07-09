using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class VehicleTypeService : IVehicleTypeService
    {
        private readonly AutoWashDbContext _context;

        public VehicleTypeService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<VehicleTypeDTO>> GetAllAsync()
        {
            return await _context.VehicleTypes
                .Select(t => new VehicleTypeDTO
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    BaseWeight = t.BaseWeight
                }).ToListAsync();
        }

        public async Task<VehicleTypeDTO> CreateAsync(CreateVehicleTypeDTO request)
        {
            var typeName = request.Name.Trim();
            var typeExists = await _context.VehicleTypes.AnyAsync(t => t.Name.ToLower() == typeName.ToLower());
            if (typeExists) throw new BadRequestException("This vehicle type already exists.");

            var type = new VehicleType
            {
                Name = typeName,
                Description = request.Description,
                BaseWeight = request.BaseWeight
            };

            _context.VehicleTypes.Add(type);
            await _context.SaveChangesAsync();

            return new VehicleTypeDTO { Id = type.Id, Name = type.Name, Description = type.Description, BaseWeight = type.BaseWeight };
        }

        public async Task<bool> UpdateAsync(int id, CreateVehicleTypeDTO request)
        {
            var type = await _context.VehicleTypes.FindAsync(id);
            if (type == null) throw new NotFoundException("Vehicle type not found.");

            var typeName = request.Name.Trim();
            var typeExists = await _context.VehicleTypes.AnyAsync(t => t.Id != id && t.Name.ToLower() == typeName.ToLower());
            if (typeExists) throw new BadRequestException("Vehicle type name already exists.");

            type.Name = typeName;
            type.Description = request.Description;
            type.BaseWeight = request.BaseWeight;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var type = await _context.VehicleTypes.Include(t => t.Vehicles).FirstOrDefaultAsync(t => t.Id == id);
            if (type == null) throw new NotFoundException("Vehicle type not found.");

            if (type.Vehicles.Any()) throw new BadRequestException("Cannot delete this vehicle type because there are customer vehicles currently using it.");

            _context.VehicleTypes.Remove(type);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}