using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;

using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly AutoWashDbContext _context;

        public EmployeeService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<EmployeeProfileDTO> CreateEmployeeAsync(CreateEmployeeDTO createDto)
        {
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == createDto.PhoneNumber))
            {
                throw new BadRequestException("Phone number already exists.");
            }

            if (createDto.BranchId.HasValue)
            {
                var branch = await _context.Branches.FindAsync(createDto.BranchId);
                if (branch == null) throw new NotFoundException("Branch not found.");
            }

            var user = new User
            {
                PhoneNumber = createDto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(createDto.Password),
                Role = createDto.Role,
                Status = "Active",
                EmployeeProfile = new EmployeeProfile
                {
                    FullName = createDto.FullName,
                    BranchId = createDto.BranchId
                }
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new EmployeeProfileDTO
            {
                UserId = user.UserId,
                PhoneNumber = user.PhoneNumber,
                FullName = user.EmployeeProfile.FullName,
                Role = user.Role,
                BranchId = user.EmployeeProfile.BranchId,
                Status = user.Status
            };
        }

        public async Task<bool> TransferEmployeeAsync(int employeeId, TransferEmployeeDTO transferDto)
        {
            var employee = await _context.EmployeeProfiles
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

            var branch = await _context.Branches.FindAsync(transferDto.BranchId);
            if (branch == null) throw new NotFoundException("Branch not found.");

            if (employee == null)
            {
                var user = await _context.Users
                    .Include(u => u.ManagerProfile)
                    .Include(u => u.StaffProfile)
                    .FirstOrDefaultAsync(u => u.UserId == employeeId
                        && (u.Role == "Manager" || u.Role == "Staff"));

                if (user == null) throw new NotFoundException("Employee not found.");

                employee = new EmployeeProfile
                {
                    EmployeeId = user.UserId,
                    FullName = user.Role == "Manager"
                        ? user.ManagerProfile?.FullName ?? user.PhoneNumber
                        : user.StaffProfile?.FullName ?? user.PhoneNumber,
                    BranchId = transferDto.BranchId
                };

                _context.EmployeeProfiles.Add(employee);
                await _context.SaveChangesAsync();
                return true;
            }

            employee.BranchId = transferDto.BranchId;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
