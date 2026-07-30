using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Constants;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class UserServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly UserService _sut;

        public UserServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new UserService(_dbContext);
        }

        [Fact]
        public async Task GetProfileAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetProfileAsync(999));
        }

        [Fact]
        public async Task GetProfileAsync_CustomerProfile_UsesCustomerFullName()
        {
            var tier = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600100", Email = "u1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Customer Name", TierId = tier.TierId, TotalPoint = 100, PromotionPoint = 50 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetProfileAsync(user.UserId);

            Assert.Equal("Customer Name", result.FullName);
            Assert.Equal("Gold", result.TierName);
            Assert.Equal(100, result.TotalPoint);
        }

        [Fact]
        public async Task GetProfileAsync_NoProfileAtAll_FallsBackToPhoneNumber()
        {
            var user = new User { PhoneNumber = "0999600101", Email = "u2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetProfileAsync(user.UserId);

            Assert.Equal("0999600101", result.FullName);
        }

        [Fact]
        public async Task GetProfileAsync_IncludesVehicles()
        {
            var user = new User { PhoneNumber = "0999600102", Email = "u3@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            _dbContext.Vehicles.Add(new Vehicle { UserId = user.UserId, LicensePlate = "51A11111", VehicleTypeId = vehicleType.Id });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetProfileAsync(user.UserId);

            Assert.Single(result.Vehicles);
            Assert.Equal("Sedan", result.Vehicles[0].VehicleType);
        }

        [Fact]
        public async Task UpdateProfileAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateUserProfileDTO { FullName = "New Name" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateProfileAsync(999, dto));
        }

        [Fact]
        public async Task UpdateProfileAsync_UpdatesCustomerFullName()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600103", Email = "u4@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Old Name", TierId = tier.TierId });
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateUserProfileDTO { FullName = "New Name" };
            var result = await _sut.UpdateProfileAsync(user.UserId, dto);

            Assert.True(result);
            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal("New Name", profile.FullName);
        }

        [Fact]
        public async Task UpdateProfileAsync_DateOfBirthAlreadySetAndDifferent_ThrowsBadRequestException()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600104", Email = "u5@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Name", TierId = tier.TierId, DateOfBirth = new DateTime(1990, 1, 1) });
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateUserProfileDTO { DateOfBirth = new DateTime(1995, 5, 5) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateProfileAsync(user.UserId, dto));
        }

        [Fact]
        public async Task UpdateProfileAsync_DateOfBirthNotYetSet_SetsIt()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600105", Email = "u6@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Name", TierId = tier.TierId, DateOfBirth = null });
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateUserProfileDTO { DateOfBirth = new DateTime(1995, 5, 5) };
            var result = await _sut.UpdateProfileAsync(user.UserId, dto);

            Assert.True(result);
            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(new DateTime(1995, 5, 5), profile.DateOfBirth);
        }

        [Fact]
        public async Task UpdateProfileAsync_DuplicatePhone_ThrowsBadRequestException()
        {
            var user1 = new User { PhoneNumber = "0999600106", Email = "u7@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999600107", Email = "u8@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateUserProfileDTO { PhoneNumber = "0999600106" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateProfileAsync(user2.UserId, dto));
        }

        [Fact]
        public async Task UpdateProfileAsync_DuplicateEmail_ThrowsBadRequestException()
        {
            var user1 = new User { PhoneNumber = "0999600108", Email = "dupe1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999600109", Email = "other@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateUserProfileDTO { Email = "dupe1@test.com" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateProfileAsync(user2.UserId, dto));
        }

        [Fact]
        public async Task UpdateProfileAsync_NoActualChanges_StillReturnsTrue()
        {
            var user = new User { PhoneNumber = "0999600110", Email = "u9@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateUserProfileDTO(); // nothing set

            var result = await _sut.UpdateProfileAsync(user.UserId, dto);

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAccountAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAccountAsync(999));
        }

        [Fact]
        public async Task DeleteAccountAsync_AlreadyDeleted_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999600200", Email = "d1@test.com", PasswordHash = "x", Role = "Customer", Status = UserStatuses.Deleted };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAccountAsync(user.UserId));
        }

        [Fact]
        public async Task DeleteAccountAsync_HasActiveBooking_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999600201", Email = "d2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.Bookings.Add(new Booking { UserId = user.UserId, LicensePlate = "51D11111", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAccountAsync(user.UserId));
        }

        [Fact]
        public async Task DeleteAccountAsync_Valid_SoftDeletesUserVehiclesAndWallet()
        {
            var user = new User { PhoneNumber = "0999600202", Email = "d3@test.com", PasswordHash = "x", Role = "Customer", Status = "Active", RefreshToken = "some-token" };
            _dbContext.Users.Add(user);
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            _dbContext.Vehicles.Add(new Vehicle { UserId = user.UserId, LicensePlate = "51D22222", VehicleTypeId = vehicleType.Id, IsDeleted = false });
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 0, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteAccountAsync(user.UserId);

            Assert.True(result);
            var updatedUser = await _dbContext.Users.FirstAsync(u => u.UserId == user.UserId);
            Assert.Equal(UserStatuses.Deleted, updatedUser.Status);
            Assert.Null(updatedUser.RefreshToken);

            var vehicle = await _dbContext.Vehicles.FirstAsync(v => v.UserId == user.UserId);
            Assert.True(vehicle.IsDeleted);

            var wallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal("Blocked", wallet.Status);
        }

        [Fact]
        public async Task GetAllCustomersAsync_FiltersByKeyword()
        {
            var user1 = new User { PhoneNumber = "0999600300", Email = "alpha@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999600301", Email = "beta@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllCustomersAsync(1, 10, "alpha", null);

            Assert.Single(result.Items);
        }

        [Fact]
        public async Task GetAllCustomersAsync_FiltersByStatus()
        {
            var user1 = new User { PhoneNumber = "0999600302", Email = "u10@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999600303", Email = "u11@test.com", PasswordHash = "x", Role = "Customer", Status = "Blocked" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllCustomersAsync(1, 10, null, "Blocked");

            Assert.Single(result.Items);
            Assert.Equal("Blocked", result.Items[0].Status);
        }

        [Fact]
        public async Task GetAllCustomersAsync_PaginatesCorrectly()
        {
            for (int i = 0; i < 5; i++)
            {
                _dbContext.Users.Add(new User { PhoneNumber = $"099960040{i}", Email = $"page{i}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" });
            }
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllCustomersAsync(page: 1, pageSize: 2, null, null);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(5, result.TotalItems);
            Assert.Equal(3, result.TotalPages); // ceil(5/2)
        }

        [Fact]
        public async Task GetCustomerDetailByAdminAsync_NotFoundOrWrongRole_ThrowsNotFoundException()
        {
            var staffUser = new User { PhoneNumber = "0999600500", Email = "s1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetCustomerDetailByAdminAsync(staffUser.UserId));
        }

        [Fact]
        public async Task GetCustomerDetailByAdminAsync_Valid_ReturnsProfile()
        {
            var user = new User { PhoneNumber = "0999600501", Email = "c1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetCustomerDetailByAdminAsync(user.UserId);

            Assert.Equal(user.UserId, result.UserId);
        }

        [Fact]
        public async Task UpdateCustomerStatusAsync_NotFoundOrWrongRole_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateCustomerStatusAsync(999, "Blocked"));
        }

        [Fact]
        public async Task UpdateCustomerStatusAsync_SameStatus_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999600502", Email = "c2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateCustomerStatusAsync(user.UserId, "Active"));
        }

        [Fact]
        public async Task UpdateCustomerStatusAsync_Valid_UpdatesStatus()
        {
            var user = new User { PhoneNumber = "0999600503", Email = "c3@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.UpdateCustomerStatusAsync(user.UserId, "Blocked");

            Assert.True(result);
            var updated = await _dbContext.Users.FirstAsync(u => u.UserId == user.UserId);
            Assert.Equal("Blocked", updated.Status);
        }

        [Fact]
        public async Task SyncCustomerProfilePointsAsync_NoTargetProfiles_NoOp()
        {
            await _sut.SyncCustomerProfilePointsAsync(); // should not throw
        }

        [Fact]
        public async Task SyncCustomerProfilePointsAsync_RecalculatesFromLedger()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600600", Email = "sync1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Sync Test", TierId = tier.TierId, TotalPoint = 0, PromotionPoint = 0 });
            await _dbContext.SaveChangesAsync();

            _dbContext.PointLedgers.AddRange(
                new PointLedger { UserId = user.UserId, PointsAdded = 100, Reason = "Service completion #1", TransactionDate = DateTime.UtcNow },
                new PointLedger { UserId = user.UserId, PointsDeducted = 30, Reason = "Redeemed voucher", TransactionDate = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            await _sut.SyncCustomerProfilePointsAsync();

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(70, profile.TotalPoint); // 100 - 30
            Assert.Equal(100, profile.PromotionPoint); // from "Service completion" ledger entry
        }

        [Fact]
        public async Task SyncCustomerProfilePointsAsync_SkipsProfilesWithExistingPoints()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600601", Email = "sync2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Sync Test 2", TierId = tier.TierId, TotalPoint = 500, PromotionPoint = 200 }); // already has points
            await _dbContext.SaveChangesAsync();

            _dbContext.PointLedgers.Add(new PointLedger { UserId = user.UserId, PointsAdded = 100, Reason = "Service completion #2", TransactionDate = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            await _sut.SyncCustomerProfilePointsAsync();

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(500, profile.TotalPoint); // unchanged, not a target
        }
    }
}