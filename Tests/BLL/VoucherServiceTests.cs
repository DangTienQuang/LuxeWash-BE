using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;

namespace AutoWashPro.Tests.BLL
{
    public class VoucherServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IWalletService> _walletMock;
        private readonly VoucherService _sut;

        public VoucherServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _walletMock = new Mock<IWalletService>();
            _walletMock.Setup(w => w.DeductSpendablePointsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            _sut = new VoucherService(_dbContext, _walletMock.Object);
        }

        private Voucher BuildActiveVoucher(string code = "TEST10", decimal discount = 10000, int pointsRequired = 0, VoucherType type = VoucherType.Discount)
        {
            return new Voucher
            {
                Code = code,
                DiscountAmount = discount,
                VoucherType = type,
                CampaignType = VoucherCampaignType.Manual,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 100,
                PointsRequired = pointsRequired,
                StartDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            };
        }

        [Fact]
        public async Task GetMyVouchersAsync_ComputesIsUsedAndRemainingUsage()
        {
            var voucher = BuildActiveVoucher();
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10), UsageCount = 1, IsUsed = false });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetMyVouchersAsync(1);

            Assert.Single(result);
            Assert.True(result[0].IsUsed); // UsageCount(1) >= MaxUsagePerUser(1)
            Assert.Equal(0, result[0].RemainingUsage);
        }

        [Fact]
        public async Task RedeemVoucherAsync_VoucherNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RedeemVoucherAsync(1, 999));
        }

        [Fact]
        public async Task RedeemVoucherAsync_InactiveVoucher_ThrowsBadRequestException()
        {
            var voucher = BuildActiveVoucher();
            voucher.IsActive = false;
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RedeemVoucherAsync(1, voucher.VoucherId));
        }

        [Fact]
        public async Task RedeemVoucherAsync_ExpiredVoucher_ThrowsBadRequestException()
        {
            var voucher = BuildActiveVoucher();
            voucher.ExpiryDate = DateTime.UtcNow.AddDays(-1);
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RedeemVoucherAsync(1, voucher.VoucherId));
        }

        [Fact]
        public async Task RedeemVoucherAsync_RequiresTierUserDoesNotMeet_ThrowsBadRequestException()
        {
            var lowTier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var highTier = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 1000 };
            _dbContext.Tiers.AddRange(lowTier, highTier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0998000001", Email = "voucher1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = lowTier.TierId });
            await _dbContext.SaveChangesAsync();

            var voucher = BuildActiveVoucher();
            voucher.RequiredTierId = highTier.TierId;
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RedeemVoucherAsync(user.UserId, voucher.VoucherId));
        }

        [Fact]
        public async Task RedeemVoucherAsync_AlreadyOwned_ThrowsBadRequestException()
        {
            var voucher = BuildActiveVoucher();
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10), TriggerKey = null });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RedeemVoucherAsync(1, voucher.VoucherId));
        }

        [Fact]
        public async Task RedeemVoucherAsync_RequiresPoints_DeductsAndCreates()
        {
            var voucher = BuildActiveVoucher(pointsRequired: 50);
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            await _sut.RedeemVoucherAsync(1, voucher.VoucherId);

            _walletMock.Verify(w => w.DeductSpendablePointsAsync(1, 50, It.IsAny<string>()), Times.Once);
            var owned = await _dbContext.UserVouchers.AnyAsync(uv => uv.UserId == 1 && uv.VoucherId == voucher.VoucherId);
            Assert.True(owned);
        }

        [Fact]
        public async Task RedeemVoucherAsync_FreeVoucher_DoesNotCallWallet()
        {
            var voucher = BuildActiveVoucher(pointsRequired: 0);
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            await _sut.RedeemVoucherAsync(1, voucher.VoucherId);

            _walletMock.Verify(w => w.DeductSpendablePointsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetAllVouchersAsync_ReturnsWithCorrectRedeemCount()
        {
            var voucher = BuildActiveVoucher();
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.AddRange(
                new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10) },
                new UserVoucher { UserId = 2, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10) }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllVouchersAsync();

            Assert.Equal(2, result[0].RedeemedCount);
        }

        [Fact]
        public async Task GrantVouchersAsync_EmptyList_NoOp()
        {
            await _sut.GrantVouchersAsync(1, new List<int>());
            // No exception, nothing to assert further
        }

        [Fact]
        public async Task GrantVouchersAsync_VoucherNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GrantVouchersAsync(999, new List<int> { 1, 2 }));
        }

        [Fact]
        public async Task GrantVouchersAsync_SkipsAlreadyOwned_GrantsOnlyNew()
        {
            var voucher = BuildActiveVoucher();
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10), TriggerKey = null });
            await _dbContext.SaveChangesAsync();

            await _sut.GrantVouchersAsync(voucher.VoucherId, new List<int> { 1, 2, 3 });

            var count = await _dbContext.UserVouchers.CountAsync(uv => uv.VoucherId == voucher.VoucherId);
            Assert.Equal(3, count); // 1 existing + 2 new
        }

        [Fact]
        public async Task CreateVoucherAsync_ExpiryInPast_ThrowsBadRequestException()
        {
            var request = new CreateOrUpdateVoucherDTO { Code = "PAST1", DiscountAmount = 10000, ExpiryDate = DateTime.UtcNow.AddDays(-1) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateVoucherAsync(request));
        }

        [Fact]
        public async Task CreateVoucherAsync_DuplicateCode_ThrowsBadRequestException()
        {
            _dbContext.Vouchers.Add(BuildActiveVoucher(code: "DUP10"));
            await _dbContext.SaveChangesAsync();

            var request = new CreateOrUpdateVoucherDTO { Code = "dup10", DiscountAmount = 10000, ExpiryDate = DateTime.UtcNow.AddDays(30) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateVoucherAsync(request));
        }

        [Fact]
        public async Task CreateVoucherAsync_RequiredTierDoesNotExist_ThrowsBadRequestException()
        {
            var request = new CreateOrUpdateVoucherDTO { Code = "NEWV1", DiscountAmount = 10000, ExpiryDate = DateTime.UtcNow.AddDays(30), RequiredTierId = 999 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateVoucherAsync(request));
        }

        [Fact]
        public async Task CreateVoucherAsync_Valid_CreatesAndUppercasesCode()
        {
            var request = new CreateOrUpdateVoucherDTO { Code = "newv2", DiscountAmount = 20000, ExpiryDate = DateTime.UtcNow.AddDays(30) };

            var result = await _sut.CreateVoucherAsync(request);

            Assert.Equal("NEWV2", result.Code);
        }

        [Fact]
        public async Task UpdateVoucherAsync_NotFound_ThrowsNotFoundException()
        {
            var request = new CreateOrUpdateVoucherDTO { Code = "X", DiscountAmount = 1000, ExpiryDate = DateTime.UtcNow.AddDays(30) };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateVoucherAsync(999, request));
        }

        [Fact]
        public async Task UpdateVoucherAsync_DuplicateCodeAgainstDifferentVoucher_ThrowsBadRequestException()
        {
            var voucherA = BuildActiveVoucher(code: "AAA111");
            var voucherB = BuildActiveVoucher(code: "BBB222");
            _dbContext.Vouchers.AddRange(voucherA, voucherB);
            await _dbContext.SaveChangesAsync();

            var request = new CreateOrUpdateVoucherDTO { Code = "AAA111", DiscountAmount = 10000, ExpiryDate = DateTime.UtcNow.AddDays(30) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateVoucherAsync(voucherB.VoucherId, request));
        }

        [Fact]
        public async Task UpdateVoucherAsync_Valid_UpdatesFields()
        {
            var voucher = BuildActiveVoucher(code: "UPDME1");
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            var request = new CreateOrUpdateVoucherDTO { Code = "UPDATED1", DiscountAmount = 99000, ExpiryDate = DateTime.UtcNow.AddDays(60) };
            var result = await _sut.UpdateVoucherAsync(voucher.VoucherId, request);

            Assert.Equal("UPDATED1", result.Code);
            Assert.Equal(99000, result.DiscountAmount);
        }

        [Fact]
        public async Task DeleteVoucherAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteVoucherAsync(999));
        }

        [Fact]
        public async Task DeleteVoucherAsync_HasOwners_ThrowsBadRequestException()
        {
            var voucher = BuildActiveVoucher();
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10) });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteVoucherAsync(voucher.VoucherId));
        }

        [Fact]
        public async Task DeleteVoucherAsync_NoOwners_DeletesSuccessfully()
        {
            var voucher = BuildActiveVoucher();
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteVoucherAsync(voucher.VoucherId);

            Assert.True(result);
            var stillExists = await _dbContext.Vouchers.AnyAsync(v => v.VoucherId == voucher.VoucherId);
            Assert.False(stillExists);
        }

        [Fact]
        public async Task GenerateCompensationVoucherAsync_CreatesVoucherAndGrantsToUser()
        {
            await _sut.GenerateCompensationVoucherAsync(1);

            var granted = await _dbContext.UserVouchers.FirstOrDefaultAsync(uv => uv.UserId == 1);
            Assert.NotNull(granted);
            var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == granted.VoucherId);
            Assert.NotNull(voucher);
            Assert.Equal(30000, voucher.DiscountAmount);
        }

        [Fact]
        public async Task ConsumePhysicalVoucherAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ConsumePhysicalVoucherAsync(1, "NOPE"));
        }

        [Fact]
        public async Task ConsumePhysicalVoucherAsync_NotPhysicalGiftType_ThrowsBadRequestException()
        {
            var voucher = BuildActiveVoucher(code: "PHYS1", type: VoucherType.Discount);
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10) });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConsumePhysicalVoucherAsync(1, "PHYS1"));
        }

        [Fact]
        public async Task ConsumePhysicalVoucherAsync_Expired_ThrowsBadRequestException()
        {
            var voucher = BuildActiveVoucher(code: "PHYS2", type: VoucherType.PhysicalGift);
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddDays(-1) });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConsumePhysicalVoucherAsync(1, "PHYS2"));
        }

        [Fact]
        public async Task ConsumePhysicalVoucherAsync_Valid_IncrementsUsage()
        {
            var voucher = BuildActiveVoucher(code: "PHYS3", type: VoucherType.PhysicalGift);
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = 1, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10), UsageCount = 0 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.ConsumePhysicalVoucherAsync(1, "PHYS3");

            Assert.True(result);
            var updated = await _dbContext.UserVouchers.FirstAsync(uv => uv.UserId == 1 && uv.VoucherId == voucher.VoucherId);
            Assert.Equal(1, updated.UsageCount);
            Assert.True(updated.IsUsed);
        }
    }
}