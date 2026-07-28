using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class AnnualTierServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly AnnualTierService _sut;

        public AnnualTierServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new AnnualTierService(_dbContext);
        }

        private async Task<User> SeedUserWithProfile(int tierId, int currentYearTierPoints)
        {
            var user = new User { PhoneNumber = "0999900" + new Random().Next(100, 999), Email = $"annual{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tierId, CurrentYearTierPoints = currentYearTierPoints });
            await _dbContext.SaveChangesAsync();

            return user;
        }

        [Fact]
        public async Task ResetAnnualTiersAsync_NoProfiles_NoOp()
        {
            await _sut.ResetAnnualTiersAsync(); // should not throw
        }

        [Fact]
        public async Task ResetAnnualTiersAsync_QualifiesForHigherTier_UpgradesTierId()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(standard, gold);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(standard.TierId, currentYearTierPoints: 600);

            await _sut.ResetAnnualTiersAsync();

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(gold.TierId, profile.TierId);
        }

        [Fact]
        public async Task ResetAnnualTiersAsync_DoesNotQualifyForAnyTier_TierUnchanged()
        {
            var minTier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 100 }; // even min tier requires 100
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(minTier, gold);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(minTier.TierId, currentYearTierPoints: 10); // below even the minimum tier's threshold

            await _sut.ResetAnnualTiersAsync();

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(minTier.TierId, profile.TierId); // unchanged since no tier matched
        }

        [Fact]
        public async Task ResetAnnualTiersAsync_QualifiesForHighestEligibleTier()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            var diamond = new Tier { TierName = "Diamond", PointMultiplier = 2.0, BookingWindowDays = 14, MinAccumulatedPoints = 2000 };
            _dbContext.Tiers.AddRange(standard, gold, diamond);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(standard.TierId, currentYearTierPoints: 2500); // qualifies for both Gold and Diamond, should get Diamond (highest)

            await _sut.ResetAnnualTiersAsync();

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(diamond.TierId, profile.TierId);
        }

        [Fact]
        public async Task ResetAnnualTiersAsync_AlwaysResetsCurrentYearPointsToZero()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(standard);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(standard.TierId, currentYearTierPoints: 300);

            await _sut.ResetAnnualTiersAsync();

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(0, profile.CurrentYearTierPoints);
        }

        [Fact]
        public async Task ResetAnnualTiersAsync_MultipleProfiles_AllProcessedIndependently()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(standard, gold);
            await _dbContext.SaveChangesAsync();

            var userA = await SeedUserWithProfile(standard.TierId, currentYearTierPoints: 600); // upgrades to Gold
            var userB = await SeedUserWithProfile(gold.TierId, currentYearTierPoints: 50); // stays or downgrades logic — with FirstOrDefault on descending order, only matches Standard (0 <= 50)

            await _sut.ResetAnnualTiersAsync();

            var profileA = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == userA.UserId);
            var profileB = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == userB.UserId);

            Assert.Equal(gold.TierId, profileA.TierId);
            Assert.Equal(standard.TierId, profileB.TierId); // downgraded since only 50 points, doesn't meet Gold's 500 threshold
            Assert.Equal(0, profileA.CurrentYearTierPoints);
            Assert.Equal(0, profileB.CurrentYearTierPoints);
        }
    }
}