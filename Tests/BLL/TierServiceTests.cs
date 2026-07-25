using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class TierServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly TierService _sut;

        public TierServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new TierService(_dbContext);
        }

        private async Task<User> SeedUserWithProfile(Tier tier, int currentYearTierPoints = 0)
        {
            var user = new User { PhoneNumber = "0997" + new Random().Next(100000, 999999), Email = $"tier{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId, CurrentYearTierPoints = currentYearTierPoints });
            await _dbContext.SaveChangesAsync();

            return user;
        }

        [Fact]
        public async Task GetTiersAsync_ReturnsOrderedByMinAccumulatedPoints()
        {
            _dbContext.Tiers.AddRange(
                new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 14, MinAccumulatedPoints = 1000 },
                new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetTiersAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("Standard", result[0].TierName);
            Assert.Equal("Gold", result[1].TierName);
        }

        [Fact]
        public async Task CreateTierAsync_DuplicateName_ThrowsBadRequestException()
        {
            _dbContext.Tiers.Add(new Tier { TierName = "Gold", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 500 });
            await _dbContext.SaveChangesAsync();

            var request = new CreateTierDTO { TierName = "Gold", PointMultiplier = 2.0, BookingWindowDays = 10, MinAccumulatedPoints = 1000 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateTierAsync(request));
        }

        [Fact]
        public async Task CreateTierAsync_DuplicateMinPoints_ThrowsBadRequestException()
        {
            _dbContext.Tiers.Add(new Tier { TierName = "Gold", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 500 });
            await _dbContext.SaveChangesAsync();

            var request = new CreateTierDTO { TierName = "Platinum", PointMultiplier = 2.0, BookingWindowDays = 10, MinAccumulatedPoints = 500 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateTierAsync(request));
        }

        [Fact]
        public async Task CreateTierAsync_Valid_CreatesAndReturnsTier()
        {
            var request = new CreateTierDTO { TierName = "Diamond", PointMultiplier = 3.0, BookingWindowDays = 14, MinAccumulatedPoints = 2000 };

            var result = await _sut.CreateTierAsync(request);

            Assert.Equal("Diamond", result.TierName);
            Assert.Equal(2000, result.MinAccumulatedPoints);
        }

        [Fact]
        public async Task UpdateTierAsync_NotFound_ThrowsNotFoundException()
        {
            var request = new UpdateTierDTO { TierName = "X", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 100 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateTierAsync(999, request));
        }

        [Fact]
        public async Task UpdateTierAsync_DuplicateAgainstDifferentTier_ThrowsBadRequestException()
        {
            var tierA = new Tier { TierName = "Gold", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 500 };
            var tierB = new Tier { TierName = "Platinum", PointMultiplier = 2.0, BookingWindowDays = 10, MinAccumulatedPoints = 1000 };
            _dbContext.Tiers.AddRange(tierA, tierB);
            await _dbContext.SaveChangesAsync();

            var request = new UpdateTierDTO { TierName = "Gold", PointMultiplier = 2.0, BookingWindowDays = 10, MinAccumulatedPoints = 1000 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateTierAsync(tierB.TierId, request));
        }

        [Fact]
        public async Task UpdateTierAsync_Valid_UpdatesFields()
        {
            var tier = new Tier { TierName = "Gold", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var request = new UpdateTierDTO { TierName = "Gold Plus", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 600 };
            var result = await _sut.UpdateTierAsync(tier.TierId, request);

            Assert.Equal("Gold Plus", result.TierName);
            Assert.Equal(600, result.MinAccumulatedPoints);
        }

        [Fact]
        public async Task EvaluateAndUpgradeTierAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.EvaluateAndUpgradeTierAsync(999));
        }

        [Fact]
        public async Task EvaluateAndUpgradeTierAsync_NoUpgradeEligible_ReturnsNull()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(tier, currentYearTierPoints: 10);

            var result = await _sut.EvaluateAndUpgradeTierAsync(user.UserId);

            Assert.Null(result);
        }

        [Fact]
        public async Task EvaluateAndUpgradeTierAsync_Eligible_UpgradesAndSaves()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(standard, gold);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(standard, currentYearTierPoints: 600);

            var result = await _sut.EvaluateAndUpgradeTierAsync(user.UserId);

            Assert.NotNull(result);
            Assert.Equal("Standard", result.OldTierName);
            Assert.Equal("Gold", result.NewTierName);

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(gold.TierId, profile.TierId);
        }

        [Fact]
        public async Task EvaluateTierForProfileAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.EvaluateTierForProfileAsync(999));
        }

        [Fact]
        public async Task EvaluateTierForProfileAsync_NoEligibleTierBelowThreshold_ReturnsNull()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 100 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(tier, currentYearTierPoints: 5); // below even Standard's own threshold

            var result = await _sut.EvaluateTierForProfileAsync(user.UserId);

            Assert.Null(result);
        }

        [Fact]
        public async Task EvaluateTierForProfileAsync_AlreadyAtEligibleTier_ReturnsNull()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(standard, gold);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(gold, currentYearTierPoints: 600); // already Gold, and still eligible for Gold

            var result = await _sut.EvaluateTierForProfileAsync(user.UserId);

            Assert.Null(result);
        }

        [Fact]
        public async Task EvaluateTierForProfileAsync_GenuineUpgrade_ReturnsResultAndUpdatesTierId()
        {
            var standard = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var gold = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(standard, gold);
            await _dbContext.SaveChangesAsync();

            var user = await SeedUserWithProfile(standard, currentYearTierPoints: 750);

            var result = await _sut.EvaluateTierForProfileAsync(user.UserId);

            Assert.NotNull(result);
            Assert.Equal("Standard", result.OldTierName);
            Assert.Equal("Gold", result.NewTierName);
        }
    }
}