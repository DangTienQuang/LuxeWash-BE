using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Enums;

namespace AutoWashPro.Tests
{
    public class VoucherServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly Mock<IWalletService> _mockWalletService;
        private readonly VoucherService _voucherService;

        public VoucherServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AutoWashDbContext(options);
            _mockWalletService = new Mock<IWalletService>(MockBehavior.Default);

            _voucherService = new VoucherService(_context, _mockWalletService.Object);
        }

        private async Task SeedBaseDataAsync(int userId, int voucherId, int tierId, string voucherCode, int pointsRequired)
        {
            _context.Users.Add(new User { UserId = userId, Email = "vouchertest@test.com", PasswordHash = "hash", Role = "Customer", Status = "Active", PhoneNumber = "0123456789" });
            
            _context.Tiers.Add(new Tier { TierId = tierId, TierName = "Gold", MinAccumulatedPoints = 1000 });
            _context.CustomerProfiles.Add(new CustomerProfile { UserId = userId, ProfileId = userId, TotalPoint = 5000, TierId = tierId, FullName = "Test User" });

            _context.Vouchers.Add(new Voucher
            {
                VoucherId = voucherId,
                Code = voucherCode,
                IsActive = true,
                ExpiryDate = DateTime.UtcNow.AddDays(30),
                PointsRequired = pointsRequired,
                MaxUsages = 100,
                CurrentUsageCount = 0,
                RequiredTierId = tierId,
                VoucherType = VoucherType.Discount,
                DiscountAmount = 50000,
                MaxUsagePerUser = 1
            });

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task RedeemVoucherAsync_ValidVoucher_AddsToUser_TC1()
        {
            // Arrange
            int userId = 10, voucherId = 10, tierId = 10;
            await SeedBaseDataAsync(userId, voucherId, tierId, "CODE1", 500);

            _mockWalletService.Setup(w => w.DeductSpendablePointsAsync(userId, 500, It.IsAny<string>())).Returns(Task.CompletedTask);

            // Act
            await _voucherService.RedeemVoucherAsync(userId, voucherId);

            // Assert
            var userVoucher = await _context.UserVouchers.FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == voucherId);
            Assert.NotNull(userVoucher);
            Assert.False(userVoucher.IsUsed);
            _mockWalletService.Verify(w => w.DeductSpendablePointsAsync(userId, 500, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RedeemVoucherAsync_InsufficientPoints_ThrowsException_TC2()
        {
            // Arrange
            int userId = 20, voucherId = 20, tierId = 20;
            await SeedBaseDataAsync(userId, voucherId, tierId, "CODE2", 50000); // Very high points required

            _mockWalletService.Setup(w => w.DeductSpendablePointsAsync(userId, 50000, It.IsAny<string>()))
                .ThrowsAsync(new BadRequestException("Insufficient points")); // Simulating WalletService throwing

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => _voucherService.RedeemVoucherAsync(userId, voucherId));
            Assert.Contains("Insufficient points", exception.Message);
        }

        [Fact]
        public async Task ConsumePhysicalVoucherAsync_ValidCode_AppliesToUser_TC3()
        {
            // Arrange
            int userId = 30, voucherId = 30, tierId = 30;
            string code = "PHYSICAL1";
            await SeedBaseDataAsync(userId, voucherId, tierId, code, 0);

            var voucher = await _context.Vouchers.FindAsync(voucherId);
            voucher.VoucherType = VoucherType.PhysicalGift;
            
            _context.UserVouchers.Add(new UserVoucher
            {
                UserId = userId,
                VoucherId = voucherId,
                IsUsed = false,
                UsageCount = 0,
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _voucherService.ConsumePhysicalVoucherAsync(userId, code);

            // Assert
            Assert.True(result);
            var updatedUserVoucher = await _context.UserVouchers.FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == voucherId);
            Assert.NotNull(updatedUserVoucher);
            Assert.Equal(1, updatedUserVoucher.UsageCount);
            Assert.True(updatedUserVoucher.IsUsed); // Because MaxUsagePerUser is 1
        }
    }
}
