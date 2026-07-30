using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;

using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class BranchService : IBranchService
    {
        private readonly AutoWashDbContext _context;

        public BranchService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<BranchDTO>> GetAllBranchesAsync()
        {
            var branches = await _context.Branches.ToListAsync();
            return branches.Select(b => new BranchDTO
            {
                BranchId = b.BranchId,
                Name = b.Name,
                Address = b.Address,
                IsActive = b.IsActive
            }).ToList();
        }

        public async Task<BranchDTO> GetBranchByIdAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) throw new NotFoundException("Branch not found.");

            return new BranchDTO
            {
                BranchId = branch.BranchId,
                Name = branch.Name,
                Address = branch.Address,
                IsActive = branch.IsActive
            };
        }

        public async Task<BranchDTO> CreateBranchAsync(CreateBranchDTO createDto)
        {
            var branch = new Branch
            {
                Name = createDto.Name,
                Address = createDto.Address,
                IsActive = true
            };

            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            return new BranchDTO
            {
                BranchId = branch.BranchId,
                Name = branch.Name,
                Address = branch.Address,
                IsActive = branch.IsActive
            };
        }

        public async Task<BranchDTO> UpdateBranchAsync(int branchId, UpdateBranchDTO updateDto)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch == null) throw new NotFoundException("Branch not found.");

            branch.Name = updateDto.Name;
            branch.Address = updateDto.Address;
            branch.IsActive = updateDto.IsActive;

            await _context.SaveChangesAsync();

            return new BranchDTO
            {
                BranchId = branch.BranchId,
                Name = branch.Name,
                Address = branch.Address,
                IsActive = branch.IsActive
            };
        }

        public async Task<BranchEmployeeSummaryDTO> GetBranchEmployeeSummaryAsync(int branchId)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
            if (!branchExists)
            {
                throw new NotFoundException("Branch not found.");
            }

            var employees = await _context.EmployeeProfiles
                .Include(e => e.User)
                .Where(e => e.BranchId == branchId)
                .ToListAsync();

            var managers = employees
                .Where(e => e.User.Role == "Manager")
                .Select(e => new EmployeeProfileDTO
                {
                    UserId = e.EmployeeId,
                    PhoneNumber = e.User.PhoneNumber,
                    FullName = e.FullName,
                    Role = e.User.Role,
                    Status = e.User.Status,
                    BranchId = e.BranchId
                })
                .ToList();

            var staff = employees
                .Where(e => e.User.Role == "Staff")
                .Select(e => new EmployeeProfileDTO
                {
                    UserId = e.EmployeeId,
                    PhoneNumber = e.User.PhoneNumber,
                    FullName = e.FullName,
                    Role = e.User.Role,
                    Status = e.User.Status,
                    BranchId = e.BranchId
                })
                .ToList();

            return new BranchEmployeeSummaryDTO
            {
                TotalManagers = managers.Count,
                TotalStaff = staff.Count,
                Managers = managers,
                Staff = staff
            };
        }
    }
}
