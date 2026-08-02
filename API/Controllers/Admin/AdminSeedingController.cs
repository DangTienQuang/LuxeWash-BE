using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace API.Controllers.Admin
{
    [Route("api/v1/admin/seeding")]
    [ApiController]
    // Uncomment the line below to secure this endpoint in production
    // [Authorize(Roles = "Admin")]
    public class AdminSeedingController : ControllerBase
    {
        private readonly AutoWashDbContext _context;

        public AdminSeedingController(AutoWashDbContext context)
        {
            _context = context;
        }

        [HttpPost("seed-tiers")]
        public async Task<IActionResult> SeedAllTierCustomers()
        {
            // 1. Ensure all tiers exist
            var tiers = new[] { 
                new Tier { TierName = "Standard", MinAccumulatedPoints = 0, PointMultiplier = 1.0, BookingWindowDays = 7 },
                new Tier { TierName = "Silver", MinAccumulatedPoints = 500, PointMultiplier = 1.2, BookingWindowDays = 14 },
                new Tier { TierName = "Gold", MinAccumulatedPoints = 2000, PointMultiplier = 1.5, BookingWindowDays = 21 },
                new Tier { TierName = "Platinum", MinAccumulatedPoints = 5000, PointMultiplier = 2.0, BookingWindowDays = 30 },
                new Tier { TierName = "Diamond", MinAccumulatedPoints = 10000, PointMultiplier = 2.5, BookingWindowDays = 60 }
            };

            foreach (var tier in tiers)
            {
                var existingTier = await _context.Tiers.FirstOrDefaultAsync(t => t.TierName == tier.TierName);
                if (existingTier == null)
                {
                    _context.Tiers.Add(tier);
                }
                else
                {
                    existingTier.MinAccumulatedPoints = tier.MinAccumulatedPoints;
                    existingTier.PointMultiplier = tier.PointMultiplier;
                    existingTier.BookingWindowDays = tier.BookingWindowDays;
                    _context.Tiers.Update(existingTier);
                }
            }
            await _context.SaveChangesAsync();

            var dbTiers = await _context.Tiers.ToListAsync();
            var seededUsers = new System.Collections.Generic.List<object>();

            // Ensure at least one VehicleType exists
            var defaultVehicleType = await _context.VehicleTypes.FirstOrDefaultAsync();
            if (defaultVehicleType == null)
            {
                defaultVehicleType = new VehicleType
                {
                    Name = "Sedan",
                    Description = "Standard 4-seater",
                    BaseWeight = 1
                };
                _context.VehicleTypes.Add(defaultVehicleType);
                await _context.SaveChangesAsync();
            }
            // 1.5 Ensure Branch, Lane, Service, ServicePrice, TimeSlot exist
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == "Test Branch VIP");
            if (branch == null)
            {
                branch = new Branch
                {
                    Name = "Test Branch VIP",
                    Address = "123 Test Street",
                    IsActive = true
                };
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();

                var standardLane = new Lane { Name = "Lane 1 - Standard", BranchId = branch.BranchId, IsVipLane = false, IsActive = true };
                var vipLane = new Lane { Name = "Lane 2 - VIP", BranchId = branch.BranchId, IsVipLane = true, IsActive = true };
                _context.Lanes.AddRange(standardLane, vipLane);

                var service = new Service { ServiceName = "Rửa Xe VIP Test", Description = "Test Service", IsActive = true };
                _context.Services.Add(service);
                await _context.SaveChangesAsync();

                var servicePrice = new ServicePrice
                {
                    ServiceId = service.ServiceId,
                    VehicleTypeId = defaultVehicleType.Id,
                    BranchId = branch.BranchId,
                    Price = 50000,
                    CapacityWeight = 1,
                    EstimatedDurationMinutes = 30
                };
                _context.ServicePrices.Add(servicePrice);

                var slot1 = new TimeSlot { BranchId = branch.BranchId, StartTime = new System.TimeSpan(8, 0, 0), EndTime = new System.TimeSpan(9, 0, 0), MaxCapacity = 5, IsVipOnly = false };
                var slot2 = new TimeSlot { BranchId = branch.BranchId, StartTime = new System.TimeSpan(9, 0, 0), EndTime = new System.TimeSpan(10, 0, 0), MaxCapacity = 2, IsVipOnly = true };
                _context.TimeSlots.AddRange(slot1, slot2);

                await _context.SaveChangesAsync();
            }

            // 2. Create test users for each tier
            var testCases = new[]
            {
                new { Phone = "0999999991", TierName = "Standard", Points = 100, LicensePlate = "51A-000.01" },
                new { Phone = "0999999992", TierName = "Silver", Points = 1000, LicensePlate = "51A-000.02" },
                new { Phone = "0999999993", TierName = "Gold", Points = 3000, LicensePlate = "51A-000.03" },
                new { Phone = "0999999994", TierName = "Platinum", Points = 6000, LicensePlate = "51A-000.04" },
                new { Phone = "0999999995", TierName = "Diamond", Points = 12000, LicensePlate = "51A-000.05" }
            };

            foreach (var testCase in testCases)
            {
                var tier = dbTiers.First(t => t.TierName == testCase.TierName);
                AutoWashPro.DAL.Entities.User? user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == testCase.Phone);
                if (user == null)
                {
                    user = new AutoWashPro.DAL.Entities.User
                    {
                        PhoneNumber = testCase.Phone,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), // Default password: 123456
                        Role = "Customer",
                        Status = "Active"
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == user.UserId);
                if (profile == null)
                {
                    profile = new CustomerProfile
                    {
                        UserId = user.UserId,
                        FullName = $"Test Customer ({testCase.TierName})",
                        TierId = tier.TierId,
                        TotalPoint = testCase.Points,
                        CurrentYearTierPoints = testCase.Points
                    };
                    _context.CustomerProfiles.Add(profile);
                }
                else
                {
                    profile.TierId = tier.TierId;
                    profile.TotalPoint = testCase.Points;
                    profile.CurrentYearTierPoints = testCase.Points;
                    _context.CustomerProfiles.Update(profile);
                }
                
                // Add Wallet
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == user.UserId);
                if (wallet == null)
                {
                    wallet = new Wallet
                    {
                        UserId = user.UserId,
                        Balance = 10000000, // 10 million VND
                        Status = "Active"
                    };
                    _context.Wallets.Add(wallet);
                }
                else
                {
                    wallet.Balance = 10000000;
                    _context.Wallets.Update(wallet);
                }

                // Add Vehicle
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == testCase.LicensePlate);
                if (vehicle == null)
                {
                    vehicle = new Vehicle
                    {
                        UserId = user.UserId,
                        LicensePlate = testCase.LicensePlate,
                        VehicleTypeId = defaultVehicleType.Id,
                        IsDeleted = false
                    };
                    _context.Vehicles.Add(vehicle);
                }
                else
                {
                    vehicle.UserId = user.UserId;
                    vehicle.VehicleTypeId = defaultVehicleType.Id;
                    vehicle.IsDeleted = false;
                    _context.Vehicles.Update(vehicle);
                }
                
                seededUsers.Add(new { Phone = testCase.Phone, Tier = testCase.TierName, Points = testCase.Points, LicensePlate = testCase.LicensePlate, WalletBalance = 10000000 });
            }
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                statusCode = 200, 
                message = "All tier test customers seeded successfully with Wallet and Vehicle.", 
                data = seededUsers
            });
        }
    }
}
