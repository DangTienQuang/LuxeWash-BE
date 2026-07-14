using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class UserService : IUserService
    {
        private readonly AutoWashDbContext _context;

        public UserService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfileDTO> GetProfileAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                    .ThenInclude(cp => cp.Tier)
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile)
                .Include(u => u.BusinessProfile)
                .Include(u => u.Vehicles)
                    .ThenInclude(v => v.VehicleType)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) throw new NotFoundException("User not found.");

            var fullName = user.CustomerProfile?.FullName
                        ?? user.StaffProfile?.FullName
                        ?? user.ManagerProfile?.FullName
                        ?? user.EmployeeProfile?.FullName
                        ?? user.BusinessProfile?.CompanyName
                        ?? user.PhoneNumber;

            return new UserProfileDTO
            {
                UserId = user.UserId,
                Email = user.Email,     
                Status = user.Status,   
                FullName = fullName,
                PhoneNumber = user.PhoneNumber,
                TierName = user.CustomerProfile?.Tier?.TierName,
                TotalPoint = user.CustomerProfile?.TotalPoint ?? 0,
                PromotionPoint = user.CustomerProfile?.PromotionPoint ?? 0,
                ChurnScore = user.CustomerProfile?.ChurnScore ?? 0,
                Vehicles = user.Vehicles.Select(v => new VehicleDTO
                {
                    LicensePlate = v.LicensePlate,
                    VehicleType = v.VehicleType?.Name
                }).ToList(),
                DateOfBirth = user.CustomerProfile?.DateOfBirth
            };
        }
        public async Task<bool> UpdateProfileAsync(int userId, UpdateUserProfileDTO request)
        {
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile)
                .Include(u => u.BusinessProfile)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                throw new NotFoundException("User data not found.");

            bool isUpdated = false;
            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                var newName = request.FullName.Trim();
                if (user.CustomerProfile != null && user.CustomerProfile.FullName != newName)
                {
                    user.CustomerProfile.FullName = newName;
                    isUpdated = true;
                }
                if (user.StaffProfile != null && user.StaffProfile.FullName != newName)
                {
                    user.StaffProfile.FullName = newName;
                    isUpdated = true;
                }
                if (user.ManagerProfile != null && user.ManagerProfile.FullName != newName)
                {
                    user.ManagerProfile.FullName = newName;
                    isUpdated = true;
                }
                if (user.EmployeeProfile != null && user.EmployeeProfile.FullName != newName)
                {
                    user.EmployeeProfile.FullName = newName;
                    isUpdated = true;
                }
                if (user.BusinessProfile != null && user.BusinessProfile.CompanyName != newName)
                {
                    user.BusinessProfile.CompanyName = newName;
                    isUpdated = true;
                }
            }
            if (request.DateOfBirth.HasValue && user.CustomerProfile != null)
            {
                if (user.CustomerProfile.DateOfBirth.HasValue && user.CustomerProfile.DateOfBirth.Value.Date != request.DateOfBirth.Value.Date)
                {
                    throw new BadRequestException("You cannot change your birth date after it has been set. Please contact Admin if you need assistance.");
                }

                if (!user.CustomerProfile.DateOfBirth.HasValue)
                {
                    user.CustomerProfile.DateOfBirth = request.DateOfBirth.Value.Date;
                    isUpdated = true;
                }
            }
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && user.PhoneNumber != request.PhoneNumber.Trim())
            {
                string newPhone = request.PhoneNumber.Trim();
                bool phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == newPhone && u.UserId != userId);
                if (phoneExists)
                    throw new BadRequestException("This phone number is already used by another account.");

                user.PhoneNumber = newPhone;
                isUpdated = true;
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && user.Email != request.Email.Trim())
            {
                string newEmail = request.Email.Trim().ToLower();

                bool emailExists = await _context.Users.AnyAsync(u => u.Email == newEmail && u.UserId != userId);
                if (emailExists)
                    throw new BadRequestException("This email is already used by another account.");

                user.Email = newEmail;
                isUpdated = true;
            }

            if (isUpdated)
            {
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> DeleteAccountAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Vehicles)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                throw new NotFoundException("User not found.");

            if (user.Status == UserStatuses.Deleted)
                throw new BadRequestException("Account is already deleted.");

            bool hasActiveBookings = await _context.Bookings.AnyAsync(b =>
                b.UserId == userId &&
                (b.Status == "Pending" || b.Status == "CheckedIn" || b.Status == "Delayed" ||
                 b.Status == "Assigned" || b.Status == "Processing" || b.Status == "Confirmed"));

            if (hasActiveBookings)
            {
                throw new BadRequestException("You currently have active bookings. Please complete or cancel them before deleting your account.");
            }

            user.Status = UserStatuses.Deleted;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;

            foreach (var vehicle in user.Vehicles.Where(v => !v.IsDeleted))
            {
                vehicle.IsDeleted = true;
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet != null)
            {
                wallet.Status = "Blocked";
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PagedResultDTO<UserAdminSummaryDTO>> GetAllCustomersAsync(int page, int pageSize, string? searchKeyword, string? statusFilter)
        {
            var query = _context.Users
                .Include(u => u.CustomerProfile)
                    .ThenInclude(cp => cp.Tier)
                .Where(u => u.Role == UserRoles.Customer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var keyword = searchKeyword.Trim().ToLower();
                query = query.Where(u => u.PhoneNumber.Contains(keyword)
                                      || (u.Email != null && u.Email.ToLower().Contains(keyword))
                                      || (u.CustomerProfile != null && u.CustomerProfile.FullName.ToLower().Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(u => u.Status == statusFilter.Trim());
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserAdminSummaryDTO
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    FullName = u.CustomerProfile != null ? u.CustomerProfile.FullName : "N/A",
                    PhoneNumber = u.PhoneNumber,
                    TierName = u.CustomerProfile != null && u.CustomerProfile.Tier != null ? u.CustomerProfile.Tier.TierName : "N/A",
                    Status = u.Status,
                    LastVisitDate = u.CustomerProfile != null ? u.CustomerProfile.LastVisitDate : null
                })
                .ToListAsync();

            return new PagedResultDTO<UserAdminSummaryDTO>
            {
                Items = users,
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = page
            };
        }

        public async Task<UserProfileDTO> GetCustomerDetailByAdminAsync(int customerId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == customerId);
            if (user == null || user.Role != UserRoles.Customer) throw new NotFoundException("Customer not found.");

            return await GetProfileAsync(customerId);
        }

        public async Task<bool> UpdateCustomerStatusAsync(int customerId, string newStatus)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == customerId);
            if (user == null || user.Role != UserRoles.Customer) throw new NotFoundException("Customer not found.");

            if (user.Status == newStatus) throw new BadRequestException($"Account is already in status {newStatus}.");

            user.Status = newStatus;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task SyncCustomerProfilePointsAsync()
        {
            const string completionPrefix = "Service completion";
            var now = DateTime.UtcNow;

            // Get users with 0 TotalPoint & 0 PromotionPoint
            var targetUsersQuery = _context.CustomerProfiles
                .Where(p => p.TotalPoint == 0 && p.PromotionPoint == 0)
                .Select(p => p.UserId);

            // Using grouped queries to efficiently calculate sums on the DB side
            var pointCalculations = await _context.PointLedgers
                .Where(pl => targetUsersQuery.Contains(pl.UserId))
                .GroupBy(pl => pl.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalAdded = g.Where(p => p.PointsAdded > 0 && (p.ExpiryDate == null || p.ExpiryDate > now)).Sum(p => p.PointsAdded),
                    TotalDeducted = g.Where(p => p.PointsDeducted > 0).Sum(p => p.PointsDeducted),
                    PromotionFromLedger = g.Where(p => p.PointsAdded > 0 && p.Reason != null && p.Reason.StartsWith(completionPrefix)).Sum(p => p.PointsAdded)
                })
                .ToListAsync();

            if (!pointCalculations.Any())
                return;

            // Load just the profiles that need updating
            var userIdsToUpdate = pointCalculations.Select(p => p.UserId).ToList();
            var profilesToUpdate = await _context.CustomerProfiles
                .Where(p => userIdsToUpdate.Contains(p.UserId))
                .ToListAsync();

            foreach (var profile in profilesToUpdate)
            {
                var calc = pointCalculations.First(c => c.UserId == profile.UserId);
                profile.TotalPoint = Math.Max(0, calc.TotalAdded - calc.TotalDeducted);
                profile.PromotionPoint = calc.PromotionFromLedger;
            }

            await _context.SaveChangesAsync();
        }
    }
}