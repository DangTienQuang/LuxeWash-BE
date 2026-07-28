using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/demo-seeder")]
    [AllowAnonymous] // Allow anyone to call this for demo purposes, or can change to [Authorize(Roles = "Admin")]
    public class AdminDemoSeederController : ControllerBase
    {
        private readonly AutoWashDbContext _context;

        public AdminDemoSeederController(AutoWashDbContext context)
        {
            _context = context;
        }

        [HttpPost("seed-revenue-data/{branchId}")]
        public async Task<IActionResult> SeedRevenueData(int branchId)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
            if (branch == null)
            {
                return NotFound(new { message = $"Branch {branchId} not found." });
            }

            var now = DateTime.UtcNow;
            
            // Generate some random users for demo
            var newUsers = new List<AutoWashPro.DAL.Entities.User>();
            for (int i = 1; i <= 5; i++)
            {
                var user = new AutoWashPro.DAL.Entities.User
                {
                    Email = $"demo{i}_{Guid.NewGuid().ToString().Substring(0, 4)}@smartwash.com",
                    PhoneNumber = $"098{new Random().Next(1000000, 9999999)}",
                    Role = "Customer",
                    Status = "Active",
                    PasswordHash = "hashed"
                };
                newUsers.Add(user);
            }
            _context.Users.AddRange(newUsers);
            await _context.SaveChangesAsync();

            // Setup Customer Profiles and Vehicles
            var tier = await _context.Tiers.FirstOrDefaultAsync() ?? new Tier { TierName = "Standard", MinAccumulatedPoints = 0 };
            if (tier.TierId == 0)
            {
                _context.Tiers.Add(tier);
                await _context.SaveChangesAsync();
            }

            foreach (var u in newUsers)
            {
                _context.CustomerProfiles.Add(new CustomerProfile
                {
                    UserId = u.UserId,
                    FullName = $"Demo Customer {u.UserId}",
                    TierId = tier.TierId,
                    LastVisitDate = now.AddDays(-50) // Simulate they haven't visited for a while
                });

                _context.Vehicles.Add(new Vehicle
                {
                    UserId = u.UserId,
                    LicensePlate = $"51G-{new Random().Next(10000, 99999)}",
                    CarModel = "Toyota Vios",
                    VehicleTypeId = 1 // Set default VehicleTypeId
                });
            }
            await _context.SaveChangesAsync();

            // Seed Bookings
            // 1. Last month: high revenue, these new users booked a lot.
            var lastMonth = now.AddMonths(-1);
            var bookings = new List<Booking>();

            var vehicles = await _context.Vehicles.Where(v => v.UserId.HasValue && newUsers.Select(u => u.UserId).Contains(v.UserId.Value)).ToListAsync();

            // Create 30 bookings last month (High Revenue)
            for (int i = 0; i < 30; i++)
            {
                var v = vehicles[new Random().Next(vehicles.Count)];
                bookings.Add(new Booking
                {
                    BranchId = branchId,
                    UserId = v.UserId,
                    VehicleId = v.Id,
                    LicensePlate = v.LicensePlate,
                    ScheduledTime = lastMonth.AddDays(-new Random().Next(1, 28)),
                    Status = "Completed",
                    OriginalPrice = 100000,
                    FinalAmount = 100000,
                    CreatedAt = lastMonth.AddDays(-new Random().Next(1, 28))
                });
            }

            // Create only 5 bookings this month (Revenue Drop)
            for (int i = 0; i < 5; i++)
            {
                var v = vehicles[new Random().Next(vehicles.Count)];
                bookings.Add(new Booking
                {
                    BranchId = branchId,
                    UserId = v.UserId,
                    VehicleId = v.Id,
                    LicensePlate = v.LicensePlate,
                    ScheduledTime = now.AddDays(-new Random().Next(1, 10)),
                    Status = "Completed",
                    OriginalPrice = 100000,
                    FinalAmount = 100000,
                    CreatedAt = now.AddDays(-new Random().Next(1, 10))
                });
            }

            // At-risk loyal customers (booked >2 times, but last booking was 50 days ago)
            var loyalUserId = newUsers[0].UserId;
            var loyalVehicle = vehicles.First(x => x.UserId == loyalUserId);
            for (int i = 0; i < 3; i++)
            {
                bookings.Add(new Booking
                {
                    BranchId = branchId,
                    UserId = loyalUserId,
                    VehicleId = loyalVehicle.Id,
                    LicensePlate = loyalVehicle.LicensePlate,
                    ScheduledTime = now.AddDays(-55), // Very old booking
                    Status = "Completed",
                    OriginalPrice = 150000,
                    FinalAmount = 150000,
                    CreatedAt = now.AddDays(-55)
                });
            }

            _context.Bookings.AddRange(bookings);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                statusCode = 200,
                message = "Demo revenue data seeded successfully. Now you can trigger the AI Revenue Analysis endpoint.",
                data = new
                {
                    NewUsersSeeded = newUsers.Count,
                    TotalBookingsSeeded = bookings.Count,
                    LastMonthRevenue = bookings.Where(b => b.ScheduledTime.Month == lastMonth.Month).Sum(b => b.FinalAmount),
                    CurrentMonthRevenue = bookings.Where(b => b.ScheduledTime.Month == now.Month).Sum(b => b.FinalAmount)
                }
            });
        }
    }
}
