using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using PayOS;

namespace AutoWashPro.Tests.BLL
{
    public class WalletServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<ITierService> _tierMock;
        private readonly Mock<IEmailService> _emailMock;
        private readonly Mock<ILogger<WalletService>> _loggerMock;
        private readonly WalletService _sut;

        public WalletServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _tierMock = new Mock<ITierService>();
            _tierMock.Setup(t => t.EvaluateTierForProfileAsync(It.IsAny<int>()))
                .ReturnsAsync(new TierUpgradeResultDTO { OldTierName = "Standard", NewTierName = "Standard" });
            _emailMock = new Mock<IEmailService>();
            _loggerMock = new Mock<ILogger<WalletService>>();

            // PayOSClient is a concrete SDK class — safe to pass null since none of these
            // tests exercise the two PayOS-dependent branches (documented gap).
            _sut = new WalletService(_dbContext, null!, _loggerMock.Object, _tierMock.Object, _emailMock.Object);
        }

        private async Task<User> SeedUser()
        {
            var user = new User { PhoneNumber = "0996" + new Random().Next(100000, 999999), Email = $"wallet{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return user;
        }

        [Fact]
        public async Task GetWalletInfoAsync_NoWallet_CreatesOneWithZeroBalance()
        {
            var user = await SeedUser();

            var result = await _sut.GetWalletInfoAsync(user.UserId);

            Assert.Equal(0, result.Balance);
            var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == user.UserId);
            Assert.NotNull(wallet);
        }

        [Fact]
        public async Task GetWalletInfoAsync_ExistingWallet_ReturnsBalance()
        {
            var user = await SeedUser();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 250000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetWalletInfoAsync(user.UserId);

            Assert.Equal(250000, result.Balance);
        }

        [Fact]
        public async Task GetWalletInfoAsync_NoProfile_ReturnsZeroPoints()
        {
            var user = await SeedUser();

            var result = await _sut.GetWalletInfoAsync(user.UserId);

            Assert.Equal(0, result.TotalPoints);
            Assert.Equal(0, result.PromotionPoints);
        }

        [Fact]
        public async Task GetWalletInfoAsync_WithProfile_ReturnsPoints()
        {
            var user = await SeedUser();
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId, TotalPoint = 500, PromotionPoint = 200 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetWalletInfoAsync(user.UserId);

            Assert.Equal(500, result.TotalPoints);
            Assert.Equal(200, result.PromotionPoints);
        }

        [Fact]
        public async Task CreatePaymentQrAsync_UserNotFound_ThrowsNotFoundException()
        {
            var request = new PaymentQrRequestDTO { PaymentType = "Topup", Amount = 100000, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreatePaymentQrAsync(999, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_TopupZeroAmount_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var request = new PaymentQrRequestDTO {PaymentType = "Topup", Amount = 0, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_TopupNullAmount_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var request = new PaymentQrRequestDTO {PaymentType = "Topup", Amount = null, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_BookingPaymentNoBookingId_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var request = new PaymentQrRequestDTO { PaymentType = "BookingPayment", CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var user = await SeedUser();
            var request = new PaymentQrRequestDTO {PaymentType = "BookingPayment", BookingId = 999, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_CancelledBooking_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51X11111", Status = "Cancelled", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var request = new PaymentQrRequestDTO {PaymentType = "BookingPayment", BookingId = booking.BookingId, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_AlreadyPaid_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51X22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var request = new PaymentQrRequestDTO {PaymentType = "BookingPayment", BookingId = booking.BookingId, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreatePaymentQrAsync_ZeroFinalAmount_ReturnsEarlyWithNoPaymentUrl()
        {
            var user = await SeedUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51X33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var request = new PaymentQrRequestDTO {PaymentType = "BookingPayment", BookingId = booking.BookingId, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            var result = await _sut.CreatePaymentQrAsync(user.UserId, request);

            Assert.Equal("", result.PaymentUrl);
            Assert.Equal(0, result.Amount);
            Assert.Equal(booking.BookingId, result.BookingId);
        }

        [Fact]
        public async Task CreatePaymentQrAsync_InvalidPaymentType_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var request = new PaymentQrRequestDTO {PaymentType = "Crypto", Amount = 100000, CancelUrl = "https://cancel", ReturnUrl = "https://return" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreatePaymentQrAsync(user.UserId, request));
        }

        [Fact]
        public async Task GetTransactionsAsync_NoWallet_ReturnsEmptyList()
        {
            var user = await SeedUser();

            var result = await _sut.GetTransactionsAsync(user.UserId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTransactionsAsync_ReturnsOrderedByCreatedAtDescending()
        {
            var user = await SeedUser();
            var wallet = new Wallet { UserId = user.UserId, Balance = 0, Status = "Active" };
            _dbContext.Wallets.Add(wallet);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.AddRange(
                new Transaction { WalletId = wallet.WalletId, Amount = 10000, TransactionType = "Topup", Status = "Completed", Description = "old", CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new Transaction { WalletId = wallet.WalletId, Amount = 20000, TransactionType = "Topup", Status = "Completed", Description = "new", CreatedAt = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetTransactionsAsync(user.UserId);

            Assert.Equal(2, result.Count);
            Assert.Equal("new", result[0].Description);
        }

        [Fact]
        public async Task GetPointsHistoryAsync_ReturnsOrderedByTransactionDateDescending()
        {
            var user = await SeedUser();
            _dbContext.PointLedgers.AddRange(
                new PointLedger { UserId = user.UserId, PointsAdded = 10, Reason = "old", TransactionDate = DateTime.UtcNow.AddDays(-1) },
                new PointLedger { UserId = user.UserId, PointsAdded = 20, Reason = "new", TransactionDate = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetPointsHistoryAsync(user.UserId);

            Assert.Equal(2, result.Count);
            Assert.Equal("new", result[0].Reason);
        }

        [Fact]
        public async Task DeductSpendablePointsAsync_ZeroOrNegative_ThrowsBadRequestException()
        {
            var user = await SeedUser();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeductSpendablePointsAsync(user.UserId, 0, "test"));
        }

        [Fact]
        public async Task DeductSpendablePointsAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeductSpendablePointsAsync(999, 10, "test"));
        }

        [Fact]
        public async Task DeductSpendablePointsAsync_InsufficientPoints_ThrowsBadRequestException()
        {
            var user = await SeedUser();
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId, TotalPoint = 5 });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeductSpendablePointsAsync(user.UserId, 10, "test"));
        }

        [Fact]
        public async Task DeductSpendablePointsAsync_Valid_DeductsAndLogsLedger()
        {
            var user = await SeedUser();
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId, TotalPoint = 100 });
            await _dbContext.SaveChangesAsync();

            await _sut.DeductSpendablePointsAsync(user.UserId, 30, "used on booking");

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(70, profile.TotalPoint);
            var ledger = await _dbContext.PointLedgers.FirstOrDefaultAsync(l => l.UserId == user.UserId);
            Assert.NotNull(ledger);
            Assert.Equal(30, ledger.PointsDeducted);
        }

        [Fact]
        public async Task RefundSpendablePointsAsync_ZeroOrNegative_ThrowsBadRequestException()
        {
            var user = await SeedUser();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RefundSpendablePointsAsync(user.UserId, 0, "test"));
        }

        [Fact]
        public async Task RefundSpendablePointsAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RefundSpendablePointsAsync(999, 10, "test"));
        }

        [Fact]
        public async Task RefundSpendablePointsAsync_Valid_AddsPointsAndLogsLedgerWithBookingRef()
        {
            var user = await SeedUser();
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId, TotalPoint = 50 });
            await _dbContext.SaveChangesAsync();

            await _sut.RefundSpendablePointsAsync(user.UserId, 20, "cancelled booking", 123);

            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(70, profile.TotalPoint);
            var ledger = await _dbContext.PointLedgers.FirstOrDefaultAsync(l => l.UserId == user.UserId);
            Assert.Equal(123, ledger.ReferenceBookingId);
        }

        [Fact]
        public async Task AwardCompletionPointsAsync_ZeroOrNegative_ThrowsBadRequestException()
        {
            var user = await SeedUser();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AwardCompletionPointsAsync(user.UserId, 0, 1));
        }

        [Fact]
        public async Task AwardCompletionPointsAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.AwardCompletionPointsAsync(999, 50, 1));
        }

        [Fact]
        public async Task AwardCompletionPointsAsync_Valid_UpdatesAllPointFieldsAndEvaluatesTier()
        {
            var user = await SeedUser();
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId, TotalPoint = 10, PromotionPoint = 5, CurrentYearTierPoints = 100 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.AwardCompletionPointsAsync(user.UserId, 50, 999);

            Assert.Equal(50, result);
            var profile = await _dbContext.CustomerProfiles.FirstAsync(p => p.UserId == user.UserId);
            Assert.Equal(60, profile.TotalPoint);
            Assert.Equal(55, profile.PromotionPoint);
            Assert.Equal(150, profile.CurrentYearTierPoints);

            var ledger = await _dbContext.PointLedgers.FirstOrDefaultAsync(l => l.ReferenceBookingId == 999);
            Assert.NotNull(ledger);
            _tierMock.Verify(t => t.EvaluateTierForProfileAsync(user.UserId), Times.Once);
        }

        [Fact]
        public async Task RefundBalanceAsync_ZeroOrNegative_ThrowsBadRequestException()
        {
            var user = await SeedUser();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RefundBalanceAsync(user.UserId, 0, "test"));
        }

        [Fact]
        public async Task RefundBalanceAsync_WalletNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RefundBalanceAsync(999, 50000, "test"));
        }

        [Fact]
        public async Task RefundBalanceAsync_Valid_CreditsWalletAndCreatesTransaction()
        {
            var user = await SeedUser();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 100000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            await _sut.RefundBalanceAsync(user.UserId, 50000, "goodwill refund");

            var wallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal(150000, wallet.Balance);

            var tx = await _dbContext.Transactions.FirstOrDefaultAsync(t => t.WalletId == wallet.WalletId && t.TransactionType == "Refund");
            Assert.NotNull(tx);
            Assert.Equal("goodwill refund", tx.Description);
        }
    }
}