using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Helpers;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class BookingServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IWalletService> _walletMock;
        private readonly Mock<ITierService> _tierMock;
        private readonly Mock<IEmailService> _emailMock;
        private readonly Mock<IVoucherService> _voucherMock;
        private readonly Mock<IVoucherCampaignService> _voucherCampaignMock;
        private readonly Mock<IPayOsService> _payOsMock;
        private readonly Mock<IBookingMaterialUsageService> _materialUsageMock;
        private readonly Mock<IOccupancyService> _occupancyMock;
        private readonly BookingService _sut;

        private async Task<(User user, Vehicle vehicle, TimeSlot slot, Service service)> SeedBookingPrerequisites(
    int bookingWindowDays = 7, decimal servicePrice = 200000, int capacityWeight = 3, int slotMaxCapacity = 10)
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = bookingWindowDays, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0994" + new Random().Next(100000, 999999), Email = $"cb{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Booker", TierId = tier.TierId, TotalPoint = 1000 });

            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = capacityWeight };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            var vehicle = new Vehicle { UserId = user.UserId, LicensePlate = "51A99999", VehicleTypeId = vehicleType.Id, IsDeleted = false };
            _dbContext.Vehicles.Add(vehicle);

            var service = new Service { ServiceName = "Basic Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = slotMaxCapacity, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);

            var sp = new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = servicePrice, CapacityWeight = capacityWeight };
            _dbContext.ServicePrices.Add(sp);

            await _dbContext.SaveChangesAsync();

            return (user, vehicle, slot, service);
        }

        private CreateBookingDTO BuildBookingRequest(Vehicle vehicle, TimeSlot slot, Service service, string paymentMethod = "Wallet", int? voucherId = null, int pointsToUse = 0)
        {
            return new CreateBookingDTO
            {
                BranchId = 1,
                SlotId = slot.SlotId,
                ScheduledDate = DateTime.UtcNow.AddDays(1).Date,
                VehicleId = vehicle.Id,
                LicensePlate = vehicle.LicensePlate,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = paymentMethod,
                VoucherId = voucherId,
                PointsToUse = pointsToUse
            };
        }

        private async Task<User> SeedActiveUser(double tierPointMultiplier = 1.0)
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = tierPointMultiplier, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0996" + new Random().Next(100000, 999999), Email = $"cancel{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId });
            await _dbContext.SaveChangesAsync();

            return user;
        }

        public BookingServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _walletMock = new Mock<IWalletService>();
            _tierMock = new Mock<ITierService>();
            _emailMock = new Mock<IEmailService>();
            _emailMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            _voucherMock = new Mock<IVoucherService>();
            _voucherCampaignMock = new Mock<IVoucherCampaignService>();
            _payOsMock = new Mock<IPayOsService>();
            _materialUsageMock = new Mock<IBookingMaterialUsageService>();
            _materialUsageMock.Setup(m => m.ConsumeForCompletedBookingAsync(It.IsAny<int>(), It.IsAny<int?>()))
                                .Returns(Task.CompletedTask);
            _occupancyMock = new Mock<IOccupancyService>();

            _sut = new BookingService(_dbContext, _walletMock.Object, _tierMock.Object, _emailMock.Object,
                _voucherMock.Object, _voucherCampaignMock.Object, _payOsMock.Object, _materialUsageMock.Object, _occupancyMock.Object);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_NoCustomerProfile_ThrowsNotFoundException()
        {
            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = DateTime.UtcNow.AddDays(1), VehicleTypeId = 1, ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAvailableSlotsAsync(999, request));
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_DateBeforeToday_ThrowsBadRequestException()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 7);

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = DateTime.UtcNow.AddDays(-2), VehicleTypeId = 1, ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetAvailableSlotsAsync(user.UserId, request));
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_DateBeyondBookingWindow_ThrowsBadRequestException()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 3);

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = DateTime.UtcNow.AddDays(10), VehicleTypeId = 1, ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetAvailableSlotsAsync(user.UserId, request));
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_VipOnlySlot_NonVipUser_MarkedUnavailable()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 7, tierName: "Standard", minAccumulatedPoints: 0);
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = true };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = DateTime.UtcNow.AddDays(1), VehicleTypeId = 1, ServiceIds = new List<int>() };
            var result = await _sut.GetAvailableSlotsAsync(user.UserId, request);

            Assert.False(result[0].IsAvailable);
            Assert.Equal("VIP only", result[0].Reason);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_VipOnlySlot_GoldTierUser_Available()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 7, tierName: "Gold", minAccumulatedPoints: 0);
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = true };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = DateTime.UtcNow.AddDays(1), VehicleTypeId = 1, ServiceIds = new List<int>() };
            var result = await _sut.GetAvailableSlotsAsync(user.UserId, request);

            Assert.True(result[0].IsAvailable);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_FullyBooked_EmptyCart_ReasonIsFullyBooked()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 7);
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 5, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.AddDays(1).Date;
            // BookedWeight must EXCEED MaxCapacity (strictly >), not just equal it
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate, BookedWeight = 6 });
            await _dbContext.SaveChangesAsync();

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = targetDate, VehicleTypeId = 1, ServiceIds = new List<int>() };
            var result = await _sut.GetAvailableSlotsAsync(user.UserId, request);

            Assert.False(result[0].IsAvailable);
            Assert.Equal("Fully booked", result[0].Reason);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_InsufficientCapacityForCart_ReasonMentionsCart()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 7);
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 5, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.AddDays(1).Date;
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate, BookedWeight = 4 });
            await _dbContext.SaveChangesAsync();

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = targetDate, VehicleTypeId = vehicleType.Id, ServiceIds = new List<int> { 1 } };
            var result = await _sut.GetAvailableSlotsAsync(user.UserId, request);

            Assert.False(result[0].IsAvailable);
            Assert.Equal("Insufficient capacity for your cart", result[0].Reason);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_SufficientCapacity_Available()
        {
            var (user, tier) = await SeedUserWithTier(bookingWindowDays: 7);
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var request = new CheckAvailableSlotsRequestDTO { BranchId = 1, TargetDate = DateTime.UtcNow.AddDays(1), VehicleTypeId = 1, ServiceIds = new List<int>() };
            var result = await _sut.GetAvailableSlotsAsync(user.UserId, request);

            Assert.True(result[0].IsAvailable);
            Assert.Equal("Available", result[0].Reason);
        }

        // Shared helper for this test class
        private async Task<(User user, Tier tier)> SeedUserWithTier(int bookingWindowDays, string tierName = "Standard", int minAccumulatedPoints = 0)
        {
            var tier = new Tier { TierName = tierName, PointMultiplier = 1.0, BookingWindowDays = bookingWindowDays, MinAccumulatedPoints = minAccumulatedPoints };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0993" + new Random().Next(100000, 999999), Email = $"bk{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test User", TierId = tier.TierId });
            await _dbContext.SaveChangesAsync();

            return (user, tier);
        }

        [Fact]
        public async Task CreateBookingAsync_WalletPayment_Success_DebitsWalletAndCreatesBooking()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites(servicePrice: 200000);
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 500000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, paymentMethod: "Wallet");
            var result = await _sut.CreateBookingAsync(user.UserId, request);

            Assert.Equal("Pending", result.Status);
            Assert.Equal(200000, result.FinalAmount);

            var wallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal(300000, wallet.Balance);
        }

        [Fact]
        public async Task CreateBookingAsync_WalletPayment_InsufficientBalance_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites(servicePrice: 200000);
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 50000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, paymentMethod: "Wallet");

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_InvalidPaymentMethod_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();

            var request = BuildBookingRequest(vehicle, slot, service, paymentMethod: "Bitcoin");

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();
            var request = BuildBookingRequest(vehicle, slot, service);
            request.LicensePlate = "NONEXISTENT";

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_ServiceDoesNotExist_ThrowsNotFoundException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service);
            request.ServiceIds = new List<int> { 99999 }; // no Service row exists at all

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_ServiceExistsButNotSupportedForVehicleType_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });

            // Real service exists, but no ServicePrice row for this vehicle type — hits the "not supported" branch
            var unsupportedService = new Service { ServiceName = "Premium Detail", IsActive = true };
            _dbContext.Services.Add(unsupportedService);
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service);
            request.ServiceIds = new List<int> { unsupportedService.ServiceId };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_InsufficientCapacity_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites(capacityWeight: 8, slotMaxCapacity: 10);
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });
            var targetDate = DateTime.UtcNow.AddDays(1).Date;
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service); // needs 8 more, 5+8=13 > 10 max

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_VoucherNotOwned_ThrowsNotFoundException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, voucherId: 999);

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_VoucherWrongBranch_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });

            var voucher = new Voucher
            {
                Code = "BRANCH2ONLY",
                DiscountAmount = 50000,
                VoucherType = AutoWashPro.DAL.Enums.VoucherType.Discount,
                CampaignType = AutoWashPro.DAL.Enums.VoucherCampaignType.Weather,
                BranchId = 2,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 100,
                StartDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            };
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = user.UserId, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10), IsUsed = false });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, voucherId: voucher.VoucherId);

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_VoucherExpired_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites();
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });

            var voucher = new Voucher
            {
                Code = "EXPIRED10",
                DiscountAmount = 50000,
                VoucherType = AutoWashPro.DAL.Enums.VoucherType.Discount,
                CampaignType = AutoWashPro.DAL.Enums.VoucherCampaignType.Weather,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 100,
                StartDate = DateTime.UtcNow.AddDays(-30),
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            };
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = user.UserId, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddDays(-1), IsUsed = false });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, voucherId: voucher.VoucherId);

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_ValidVoucher_AppliesDiscount()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites(servicePrice: 200000);
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });

            var voucher = new Voucher
            {
                Code = "SAVE50K",
                DiscountAmount = 50000,
                VoucherType = AutoWashPro.DAL.Enums.VoucherType.Discount,
                CampaignType = AutoWashPro.DAL.Enums.VoucherCampaignType.Weather,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 100,
                StartDate = DateTime.UtcNow.AddDays(-1),
                ExpiryDate = DateTime.UtcNow.AddDays(30)
            };
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher { UserId = user.UserId, VoucherId = voucher.VoucherId, ReceivedDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(10), IsUsed = false });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, voucherId: voucher.VoucherId);
            var result = await _sut.CreateBookingAsync(user.UserId, request);

            Assert.Equal(50000, result.VoucherDiscountAmount);
            Assert.Equal(150000, result.FinalAmount);
        }

        [Fact]
        public async Task CreateBookingAsync_PointsRequested_ButPriceTooLowForAnyPoints_ThrowsBadRequestException()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites(servicePrice: 50); // under VndPerSpendPoint (100)
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = BuildBookingRequest(vehicle, slot, service, pointsToUse: 5);

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBookingAsync(user.UserId, request));
        }

        [Fact]
        public async Task CreateBookingAsync_PointsExceedBalance_CapsAtAvailablePoints()
        {
            var (user, vehicle, slot, service) = await SeedBookingPrerequisites(servicePrice: 500000); // plenty of room for money-side cap
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            // CustomerProfile seeded with TotalPoint = 1000 in SeedBookingPrerequisites
            var request = BuildBookingRequest(vehicle, slot, service, pointsToUse: 5000); // way more than the 1000 available

            var result = await _sut.CreateBookingAsync(user.UserId, request);

            // Capped at TotalPoint (1000) * VndPerSpendPoint (100) = 100,000 VND discount
            Assert.Equal(100000, result.PointDiscountAmount);
            Assert.Equal(400000, result.FinalAmount);
        }

        private async Task<(VehicleType vehicleType, Service service)> SeedWalkInPrerequisites(decimal servicePrice = 150000, int capacityWeight = 3, int slotMaxCapacity = 10)
        {
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = capacityWeight };
            _dbContext.VehicleTypes.Add(vehicleType);

            var service = new Service { ServiceName = "Basic Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var sp = new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = servicePrice, CapacityWeight = capacityWeight };
            _dbContext.ServicePrices.Add(sp);

            var slot = new TimeSlot { BranchId = 1, StartTime = DateTime.UtcNow.TimeOfDay.Add(TimeSpan.FromMinutes(-30)), EndTime = DateTime.UtcNow.TimeOfDay.Add(TimeSpan.FromHours(2)), MaxCapacity = slotMaxCapacity, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);

            await _dbContext.SaveChangesAsync();
            return (vehicleType, service);
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_NoServiceIds_ThrowsBadRequestException()
        {
            var request = new CreateWalkInBookingDTO { BranchId = 1, LicensePlate = "51A11111", ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWalkInBookingAsync(1, request));
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_LicensePlateHasActiveBooking_ThrowsBadRequestException()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites();

            _dbContext.Bookings.Add(new Booking
            {
                LicensePlate = "51A22222",
                Status = "Pending",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.AddHours(1),
                OriginalPrice = 0,
                FinalAmount = 0
            });
            await _dbContext.SaveChangesAsync();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                LicensePlate = "51A-222.22",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Cash"
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWalkInBookingAsync(1, request));
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_Guest_CashPayment_Success()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = 0,
                LicensePlate = "51A33333",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Cash"
            };

            var result = await _sut.CreateWalkInBookingAsync(1, request);

            Assert.Equal("CheckedIn", result.Status);
            Assert.Equal(150000, result.FinalAmount);
            Assert.Null(result.PaymentUrl);

            var vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate == "51A33333");
            Assert.NotNull(vehicle);
            Assert.Null(vehicle.UserId); // guest — no linked account
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_Guest_WalletPayment_ThrowsBadRequestException()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = 0,
                LicensePlate = "51A44444",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Wallet"
            };

            // Guests have no account/wallet — Wallet payment must be rejected
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWalkInBookingAsync(1, request));
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_InvalidPaymentMethod_ThrowsBadRequestException()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = 0,
                LicensePlate = "51A55555",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Crypto"
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWalkInBookingAsync(1, request));
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_RegisteredCustomer_WalletPayment_Success()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0995000001", Email = "walkin1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Walk In Customer", TierId = tier.TierId, TotalPoint = 0 });
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 500000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = user.UserId,
                LicensePlate = "51A66666",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Wallet"
            };

            var result = await _sut.CreateWalkInBookingAsync(1, request);

            Assert.Equal("CheckedIn", result.Status);
            Assert.Equal(150000, result.FinalAmount);

            var wallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal(350000, wallet.Balance);
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_RegisteredCustomer_WalletInsufficientBalance_ThrowsBadRequestException()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0995000002", Email = "walkin2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Poor Customer", TierId = tier.TierId });
            _dbContext.Wallets.Add(new Wallet { UserId = user.UserId, Balance = 1000, Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = user.UserId,
                LicensePlate = "51A77777",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Wallet"
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWalkInBookingAsync(1, request));
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_ExistingSoftDeletedVehicle_RestoresIt()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            var deletedVehicle = new Vehicle { LicensePlate = "51A88888", VehicleTypeId = vehicleType.Id, IsDeleted = true };
            _dbContext.Vehicles.Add(deletedVehicle);
            await _dbContext.SaveChangesAsync();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = 0,
                LicensePlate = "51A88888",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Cash"
            };

            await _sut.CreateWalkInBookingAsync(1, request);

            var vehicle = await _dbContext.Vehicles.FirstAsync(v => v.LicensePlate == "51A88888");
            Assert.False(vehicle.IsDeleted);
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_NoVehicleTypeSpecified_UsesOtherType()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            // Add an "Other" VehicleType + "Other" CarModel + a matching ServicePrice, since the fallback path needs these to resolve pricing
            var otherType = new VehicleType { Name = "Other", BaseWeight = 1 };
            _dbContext.VehicleTypes.Add(otherType);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = otherType.Id, BranchId = 1, Price = 150000, CapacityWeight = 1 });
            await _dbContext.SaveChangesAsync();

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = 0,
                LicensePlate = "51A99900",
                VehicleTypeId = null,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "Cash"
            };

            var result = await _sut.CreateWalkInBookingAsync(1, request);

            Assert.Equal("CheckedIn", result.Status);
            var vehicle = await _dbContext.Vehicles.FirstAsync(v => v.LicensePlate == "51A99900");
            Assert.Equal(otherType.Id, vehicle.VehicleTypeId);
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_Guest_PayOS_Success_ReturnsPaymentUrl()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            _payOsMock
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(new PayOsPaymentResult { CheckoutUrl = "https://payos.vn/checkout/abc123", OrderCode = 123456 });

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = 0,
                LicensePlate = "51B11111",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "PayOS"
            };

            var result = await _sut.CreateWalkInBookingAsync(1, request);

            Assert.Equal("https://payos.vn/checkout/abc123", result.PaymentUrl);
            Assert.Equal("CheckedIn", result.Status);
        }

        [Fact]
        public async Task CreateWalkInBookingAsync_RegisteredCustomer_PayOS_Success_ReturnsPaymentUrl()
        {
            var (vehicleType, service) = await SeedWalkInPrerequisites(servicePrice: 150000);

            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0995000003", Email = "payoswalkin@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "PayOS Customer", TierId = tier.TierId });
            await _dbContext.SaveChangesAsync();

            _payOsMock
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(new PayOsPaymentResult { CheckoutUrl = "https://payos.vn/checkout/xyz789", OrderCode = 789012 });

            var request = new CreateWalkInBookingDTO
            {
                BranchId = 1,
                UserId = user.UserId,
                LicensePlate = "51B22222",
                VehicleTypeId = vehicleType.Id,
                ServiceIds = new List<int> { service.ServiceId },
                PaymentMethod = "PayOS"
            };

            var result = await _sut.CreateWalkInBookingAsync(1, request);

            Assert.Equal("https://payos.vn/checkout/xyz789", result.PaymentUrl);
        }

        [Fact]
        public async Task GetBookingPaymentStatusAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBookingPaymentStatusAsync(999));
        }

        [Fact]
        public async Task GetBookingPaymentStatusAsync_NoTransaction_ReturnsUnpaid()
        {
            var booking = new Booking { LicensePlate = "51C11111", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBookingPaymentStatusAsync(booking.BookingId);

            Assert.Equal("Unpaid", result.PaymentStatus);
        }

        [Fact]
        public async Task GetBookingPaymentStatusAsync_CompletedTransaction_ReturnsCompleted()
        {
            var booking = new Booking { LicensePlate = "51C22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction
            {
                ReferenceBookingId = booking.BookingId,
                TransactionType = "BookingPayment",
                Status = "Completed",
                Amount = 100000,
                CreatedAt = DateTime.UtcNow,
                Description = "Test Payment"
            });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBookingPaymentStatusAsync(booking.BookingId);

            Assert.Equal("Completed", result.PaymentStatus);
        }

        [Fact]
        public async Task GetBookingPaymentStatusAsync_PendingPayOS_VerifiedAsPaid_UpdatesToCompleted()
        {
            var booking = new Booking { LicensePlate = "51C33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var tx = new Transaction
            {
                ReferenceBookingId = booking.BookingId,
                TransactionType = "BookingPayment",
                Status = "Pending",
                PaymentMethod = "PayOS",
                OrderCode = "ORDER123",
                Amount = 100000,
                CreatedAt = DateTime.UtcNow,
                Description = "Test Payment"
            };
            _dbContext.Transactions.Add(tx);
            await _dbContext.SaveChangesAsync();

            _payOsMock.Setup(p => p.GetPaymentStatusAsync("ORDER123"))
                .ReturnsAsync(new PayOsOrderStatusResult { OrderCode = "ORDER123", Status = "PAID", Amount = 100000, PaidAt = DateTime.UtcNow });

            var result = await _sut.GetBookingPaymentStatusAsync(booking.BookingId);

            Assert.Equal("Completed", result.PaymentStatus);
        }

        [Fact]
        public async Task GetBookingPaymentStatusAsync_PendingPayOS_VerifiedAsExpired_UpdatesToExpired()
        {
            var booking = new Booking { LicensePlate = "51C44444", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var tx = new Transaction
            {
                ReferenceBookingId = booking.BookingId,
                TransactionType = "BookingPayment",
                Status = "Pending",
                PaymentMethod = "PayOS",
                OrderCode = "ORDER456",
                Amount = 100000,
                CreatedAt = DateTime.UtcNow,
                Description = "Test Payment"
            };
            _dbContext.Transactions.Add(tx);
            await _dbContext.SaveChangesAsync();

            _payOsMock.Setup(p => p.GetPaymentStatusAsync("ORDER456"))
                .ReturnsAsync(new PayOsOrderStatusResult { OrderCode = "ORDER456", Status = "EXPIRED", Amount = 100000 });

            var result = await _sut.GetBookingPaymentStatusAsync(booking.BookingId);

            Assert.Equal("Expired", result.PaymentStatus);
        }

        [Fact]
        public async Task GetBookingPaymentStatusAsync_PayOsVerificationThrows_SwallowsErrorAndReturnsPending()
        {
            var booking = new Booking { LicensePlate = "51C55555", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var tx = new Transaction
            {
                ReferenceBookingId = booking.BookingId,
                TransactionType = "BookingPayment",
                Status = "Pending",
                PaymentMethod = "PayOS",
                OrderCode = "ORDER789",
                Amount = 100000,
                CreatedAt = DateTime.UtcNow,
                Description = "Test Payment"
            };
            _dbContext.Transactions.Add(tx);
            await _dbContext.SaveChangesAsync();

            _payOsMock.Setup(p => p.GetPaymentStatusAsync("ORDER789")).ThrowsAsync(new Exception("PayOS API down"));

            var result = await _sut.GetBookingPaymentStatusAsync(booking.BookingId);

            // Exception is caught silently — status stays at whatever it resolved to before the failed verification attempt
            Assert.Equal("Pending", result.PaymentStatus);
        }

        [Fact]
        public async Task CancelBookingAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CancelBookingAsync(1, 999));
        }

        [Fact]
        public async Task CancelBookingAsync_NotPendingStatus_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51D11111", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(5), OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CancelBookingAsync(user.UserId, booking.BookingId));
        }

        [Fact]
        public async Task CancelBookingAsync_RefundableWithCompletedPayment_RefundsWallet()
        {
            var user = await SeedActiveUser();
            var wallet = new Wallet { UserId = user.UserId, Balance = 0, Status = "Active" };
            _dbContext.Wallets.Add(wallet);
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51D22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(6), OriginalPrice = 100000, FinalAmount = 100000, CapacityWeight = 3 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, WalletId = wallet.WalletId, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CancelBookingAsync(user.UserId, booking.BookingId);

            Assert.True(result);
            var updatedWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal(100000, updatedWallet.Balance);

            var updatedBooking = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("Cancelled", updatedBooking.Status);
        }

        [Fact]
        public async Task CancelBookingAsync_RefundableNoCompletedPayment_NoWalletRefund()
        {
            var user = await SeedActiveUser();
            var wallet = new Wallet { UserId = user.UserId, Balance = 0, Status = "Active" };
            _dbContext.Wallets.Add(wallet);
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51D33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(6), OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await _sut.CancelBookingAsync(user.UserId, booking.BookingId);

            var updatedWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal(0, updatedWallet.Balance);
        }

        [Fact]
        public async Task CancelBookingAsync_LateCancellation_NoRefundIssued()
        {
            var user = await SeedActiveUser();
            var wallet = new Wallet { UserId = user.UserId, Balance = 0, Status = "Active" };
            _dbContext.Wallets.Add(wallet);
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51D44444", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(1), OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, WalletId = wallet.WalletId, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CancelBookingAsync(user.UserId, booking.BookingId);

            Assert.True(result);
            var updatedWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == user.UserId);
            Assert.Equal(0, updatedWallet.Balance); // no refund — cancelled within 4hr window
        }

        [Fact]
        public async Task CancelBookingAsync_ReleasesSlotCapacity()
        {
            var user = await SeedActiveUser();
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var scheduledTime = DateTime.UtcNow.AddHours(6).Date.Add(new TimeSpan(9, 0, 0));
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51D55555", Status = "Pending", BranchId = 1, ScheduledTime = scheduledTime, OriginalPrice = 0, FinalAmount = 0, CapacityWeight = 3 };
            _dbContext.Bookings.Add(booking);

            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = scheduledTime.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            await _sut.CancelBookingAsync(user.UserId, booking.BookingId);

            var capacity = await _dbContext.DailySlotCapacities.FirstAsync(dc => dc.SlotId == slot.SlotId);
            Assert.Equal(2, capacity.BookedWeight); // 5 - 3
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateBookingStatusAsync(999, "Completed"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51E11111", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusAsync(booking.BookingId, "Teleported"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_UnpaidCannotCheckIn_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51E22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusAsync(booking.BookingId, "CheckedIn"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_CompletingBooking_AwardsPointsAndCallsMaterialUsage()
        {
            var user = await SeedActiveUser(tierPointMultiplier: 1.0);
            var booking = new Booking
            {
                UserId = user.UserId,
                LicensePlate = "51E33333",
                Status = "CheckedIn",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                ProcessingStartTime = DateTime.UtcNow.AddMinutes(-20)
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            _walletMock.Setup(w => w.AwardCompletionPointsAsync(user.UserId, It.IsAny<int>(), booking.BookingId))
                .ReturnsAsync(100); // dummy points-earned return value, not asserted on

            _voucherCampaignMock.Setup(v => v.ProcessMilestoneCampaignsAsync(user.UserId))
                .ReturnsAsync(new VoucherCampaignProcessResultDTO { VoucherCode = "TEST" });

            var result = await _sut.UpdateBookingStatusAsync(booking.BookingId, "Completed");

            Assert.True(result);
            _materialUsageMock.Verify(m => m.ConsumeForCompletedBookingAsync(booking.BookingId, It.IsAny<int?>()), Times.Once);
            _walletMock.Verify(w => w.AwardCompletionPointsAsync(user.UserId, 100, booking.BookingId), Times.Once); // 100000/1000 * 1.0 = 100 points
            _voucherCampaignMock.Verify(v => v.ProcessMilestoneCampaignsAsync(user.UserId), Times.Once);

            var updatedBooking = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal(20, updatedBooking.ActualDurationMinutes);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_CompletingWithZeroFinalAmount_NoPointsAwarded()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51E44444", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _voucherCampaignMock.Setup(v => v.ProcessMilestoneCampaignsAsync(user.UserId))
                .ReturnsAsync(new VoucherCampaignProcessResultDTO { VoucherCode = "TEST" });

            await _sut.UpdateBookingStatusAsync(booking.BookingId, "Completed");

            _walletMock.Verify(w => w.AwardCompletionPointsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_AlreadyCompleted_ReCompleting_DoesNotDoubleAwardPoints()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51E55555", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            await _sut.UpdateBookingStatusAsync(booking.BookingId, "Completed");

            _walletMock.Verify(w => w.AwardCompletionPointsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _voucherCampaignMock.Verify(v => v.ProcessMilestoneCampaignsAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetBookingByIdAsync_NotFoundOrWrongUser_ThrowsNotFoundException()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = 99999, LicensePlate = "51F11111", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBookingByIdAsync(user.UserId, booking.BookingId));
        }

        [Fact]
        public async Task GetBookingByIdAsync_FoundAndOwnedByUser_ReturnsDTO()
        {
            var user = await SeedActiveUser();
            var service = new Service { ServiceName = "Wax", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                UserId = user.UserId,
                LicensePlate = "51F22222",
                Status = "Pending",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBookingByIdAsync(user.UserId, booking.BookingId);

            Assert.Equal(booking.BookingId, result.BookingId);
            Assert.Contains("Wax", result.ServiceNames);
        }

        [Fact]
        public async Task GetMyBookingsAsync_NoBookings_ReturnsEmptyList()
        {
            var user = await SeedActiveUser();

            var result = await _sut.GetMyBookingsAsync(user.UserId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMyBookingsAsync_MultipleBookings_OrderedByScheduledTimeDescending()
        {
            var user = await SeedActiveUser();
            _dbContext.Bookings.AddRange(
                new Booking { UserId = user.UserId, LicensePlate = "51F33333", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(-2), OriginalPrice = 0, FinalAmount = 0 },
                new Booking { UserId = user.UserId, LicensePlate = "51F44444", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(1), OriginalPrice = 0, FinalAmount = 0 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetMyBookingsAsync(user.UserId);

            Assert.Equal(2, result.Count);
            Assert.Equal("51F44444", result[0].LicensePlate); // future booking first (descending)
        }

        [Fact]
        public async Task MarkAsNoShowAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.MarkAsNoShowAsync(999));
        }

        [Fact]
        public async Task MarkAsNoShowAsync_ValidBooking_SetsStatusToNoShow()
        {
            var booking = new Booking { LicensePlate = "51F55555", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await _sut.MarkAsNoShowAsync(booking.BookingId);

            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("NoShow", updated.Status);
        }

        [Fact]
        public async Task GetAllBookingsByDateAsync_NoBookings_ReturnsEmptyList()
        {
            var result = await _sut.GetAllBookingsByDateAsync(DateTime.UtcNow.AddDays(5));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllBookingsByDateAsync_BookingsExist_ReturnsOrderedWithPaymentStatus()
        {
            var targetDate = DateTime.UtcNow.Date.AddDays(3);
            var booking1 = new Booking { LicensePlate = "51F66666", Status = "Pending", BranchId = 1, ScheduledTime = targetDate.AddHours(14), OriginalPrice = 100000, FinalAmount = 100000 };
            var booking2 = new Booking { LicensePlate = "51F77777", Status = "Pending", BranchId = 1, ScheduledTime = targetDate.AddHours(9), OriginalPrice = 50000, FinalAmount = 50000 };
            _dbContext.Bookings.AddRange(booking1, booking2);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking2.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 50000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllBookingsByDateAsync(targetDate);

            Assert.Equal(2, result.Count);
            Assert.Equal("51F77777", result[0].LicensePlate); // earlier time first (ascending order this time)
            Assert.Equal("Completed", result[0].PaymentStatus);
            Assert.Equal("Unpaid", result[1].PaymentStatus);
        }

        [Fact]
        public async Task RescheduleBookingAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var user = await SeedActiveUser();
            var request = new RescheduleBookingDTO { NewSlotId = 1, NewScheduledDate = DateTime.UtcNow.AddDays(2) };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RescheduleBookingAsync(user.UserId, 999, request));
        }

        [Fact]
        public async Task RescheduleBookingAsync_NotPendingOrConfirmed_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51G11111", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(2), OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var request = new RescheduleBookingDTO { NewSlotId = 1, NewScheduledDate = DateTime.UtcNow.AddDays(3) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RescheduleBookingAsync(user.UserId, booking.BookingId, request));
        }

        [Fact]
        public async Task RescheduleBookingAsync_LessThan2HoursBeforeStart_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51G22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddMinutes(90), OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var request = new RescheduleBookingDTO { NewSlotId = 1, NewScheduledDate = DateTime.UtcNow.AddDays(2) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RescheduleBookingAsync(user.UserId, booking.BookingId, request));
        }

        [Fact]
        public async Task RescheduleBookingAsync_NewSlotWrongBranch_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51G33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(2), OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);

            var otherBranchSlot = new TimeSlot { BranchId = 2, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(otherBranchSlot);
            await _dbContext.SaveChangesAsync();

            var request = new RescheduleBookingDTO { NewSlotId = otherBranchSlot.SlotId, NewScheduledDate = DateTime.UtcNow.AddDays(3) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RescheduleBookingAsync(user.UserId, booking.BookingId, request));
        }

        [Fact]
        public async Task RescheduleBookingAsync_NewSlotInsufficientCapacity_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51G44444", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(2), OriginalPrice = 0, FinalAmount = 0, CapacityWeight = 5 };
            _dbContext.Bookings.Add(booking);

            var newSlot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 5 };
            _dbContext.TimeSlots.Add(newSlot);
            await _dbContext.SaveChangesAsync();

            var newDate = DateTime.UtcNow.AddDays(3).Date;
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = newSlot.SlotId, BranchId = 1, Date = newDate, BookedWeight = 3 }); // 3+5=8 > 5 max
            await _dbContext.SaveChangesAsync();

            var request = new RescheduleBookingDTO { NewSlotId = newSlot.SlotId, NewScheduledDate = newDate };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RescheduleBookingAsync(user.UserId, booking.BookingId, request));
        }

        [Fact]
        public async Task RescheduleBookingAsync_ValidReschedule_TransfersCapacityAndUpdatesTime()
        {
            var user = await SeedActiveUser();
            var oldSlot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            var newSlot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.AddRange(oldSlot, newSlot);
            await _dbContext.SaveChangesAsync();

            var oldScheduledTime = DateTime.UtcNow.AddDays(2).Date.Add(new TimeSpan(9, 0, 0));
            var booking = new Booking { UserId = user.UserId, LicensePlate = "51G55555", Status = "Pending", BranchId = 1, ScheduledTime = oldScheduledTime, OriginalPrice = 0, FinalAmount = 0, CapacityWeight = 4 };
            _dbContext.Bookings.Add(booking);

            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = oldSlot.SlotId, BranchId = 1, Date = oldScheduledTime.Date, BookedWeight = 4 });
            await _dbContext.SaveChangesAsync();

            var newDate = DateTime.UtcNow.AddDays(3).Date;
            var request = new RescheduleBookingDTO { NewSlotId = newSlot.SlotId, NewScheduledDate = newDate };

            var result = await _sut.RescheduleBookingAsync(user.UserId, booking.BookingId, request);

            Assert.Equal(newDate.Add(new TimeSpan(14, 0, 0)), result.ScheduledTime);

            var oldCapacity = await _dbContext.DailySlotCapacities.FirstAsync(dc => dc.SlotId == oldSlot.SlotId && dc.Date == oldScheduledTime.Date);
            Assert.Equal(0, oldCapacity.BookedWeight);

            var newCapacity = await _dbContext.DailySlotCapacities.FirstAsync(dc => dc.SlotId == newSlot.SlotId && dc.Date == newDate);
            Assert.Equal(4, newCapacity.BookedWeight);
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_InvalidStatus_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusByLicensePlateAsync("51H11111", "Teleported"));
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_NoMatchingBooking_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateBookingStatusByLicensePlateAsync("51H22222", "CheckedIn"));
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_NotScheduledToday_ThrowsNotFoundException()
        {
            var booking = new Booking { LicensePlate = "51H33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(5), OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateBookingStatusByLicensePlateAsync("51H33333", "CheckedIn"));
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_MultipleBookingsToday_ThrowsBadRequestException()
        {
            var plate = "51H44444";
            _dbContext.Bookings.AddRange(
                new Booking { LicensePlate = plate, Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(1), OriginalPrice = 0, FinalAmount = 0 },
                new Booking { LicensePlate = plate, Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(3), OriginalPrice = 0, FinalAmount = 0 }
            );
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusByLicensePlateAsync(plate, "CheckedIn"));
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_AlreadyAtTargetStatus_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51H55555", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusByLicensePlateAsync("51H55555", "CheckedIn"));
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_InvalidTransition_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51H66666", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            // Pending -> Completed directly is not a valid transition
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusByLicensePlateAsync("51H66666", "Completed"));
        }

        [Fact]
        public async Task UpdateBookingStatusByLicensePlateAsync_ValidTransition_UpdatesSuccessfully()
        {
            var booking = new Booking { LicensePlate = "51H77777", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.UpdateBookingStatusByLicensePlateAsync("51H77777", "CheckedIn");

            Assert.Equal("CheckedIn", result.Status);
        }

        [Fact]
        public async Task LookupLicensePlateAsync_InvalidPlate_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.LookupLicensePlateAsync("!!!", 1));
        }

        [Fact]
        public async Task LookupLicensePlateAsync_PreBookedToday_ReturnsPreBookedType()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                LicensePlate = "51I11111",
                Status = "Confirmed",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.ToVnTime().Date.AddHours(10),
                OriginalPrice = 0,
                FinalAmount = 0,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 0 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.LookupLicensePlateAsync("51I11111", 1);

            Assert.Equal("PreBooked", result.CustomerType);
        }

        [Fact]
        public async Task LookupLicensePlateAsync_RegisteredWalkInVehicle_ReturnsWalkInType()
        {
            var user = await SeedActiveUser();
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _dbContext.Vehicles.Add(new Vehicle { UserId = user.UserId, LicensePlate = "51I22222", VehicleTypeId = vehicleType.Id, IsDeleted = false });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.LookupLicensePlateAsync("51I22222", 1);

            Assert.Equal("WalkIn", result.CustomerType);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task LookupLicensePlateAsync_NoMatchAnywhere_ReturnsWalkInWithNullData()
        {
            var result = await _sut.LookupLicensePlateAsync("51I99999", 1);

            Assert.Equal("WalkIn", result.CustomerType);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_EmptyServiceIds_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();

            await Assert.ThrowsAsync<BadRequestException>(() =>
                _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, 1, DateTime.UtcNow.AddDays(1), null, "51J11111", new List<int>()));
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var user = await SeedActiveUser();

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, 999, DateTime.UtcNow.AddDays(1), null, "51J22222", new List<int> { 1 }));
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_VipSlotNonVipUser_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser(); // Standard tier, non-VIP
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = true };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() =>
                _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, slot.SlotId, DateTime.UtcNow.AddDays(1), null, "51J33333", new List<int> { 1 }));
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_PastDateTime_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() =>
                _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, slot.SlotId, DateTime.UtcNow.AddDays(-5), null, "51J44444", new List<int> { 1 }));
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var user = await SeedActiveUser();
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, slot.SlotId, DateTime.UtcNow.AddDays(1), null, "51J55555", new List<int> { 1 }));
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_VehicleHasActiveBooking_ThrowsBadRequestException()
        {
            var user = await SeedActiveUser();
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _dbContext.Vehicles.Add(new Vehicle { UserId = user.UserId, LicensePlate = "51J66666", VehicleTypeId = vehicleType.Id, IsDeleted = false });
            _dbContext.Bookings.Add(new Booking { LicensePlate = "51J66666", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddHours(1), OriginalPrice = 0, FinalAmount = 0 });

            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() =>
                _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, slot.SlotId, DateTime.UtcNow.AddDays(1), null, "51J66666", new List<int> { 1 }));
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_SufficientCapacity_ReturnsCompatibleTrue()
        {
            var user = await SeedActiveUser();
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            _dbContext.Vehicles.Add(new Vehicle { UserId = user.UserId, LicensePlate = "51J77777", VehicleTypeId = vehicleType.Id, IsDeleted = false });
            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 100000, CapacityWeight = 3 });

            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, slot.SlotId, DateTime.UtcNow.AddDays(1), null, "51J77777", new List<int> { service.ServiceId });

            Assert.True(result.IsCompatible);
        }

        [Fact]
        public async Task ValidateBookingCompatibilityAsync_InsufficientCapacity_ReturnsCompatibleFalse()
        {
            var user = await SeedActiveUser();
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 8 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            _dbContext.Vehicles.Add(new Vehicle { UserId = user.UserId, LicensePlate = "51J88888", VehicleTypeId = vehicleType.Id, IsDeleted = false });
            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = 1, Price = 100000, CapacityWeight = 8 });

            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = false };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.AddDays(1).Date;
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate, BookedWeight = 5 }); // 5+8=13 > 10
            await _dbContext.SaveChangesAsync();

            var result = await _sut.ValidateBookingCompatibilityAsync(user.UserId, 1, slot.SlotId, DateTime.UtcNow.AddDays(1), null, "51J88888", new List<int> { service.ServiceId });

            Assert.False(result.IsCompatible);
        }

        [Fact]
        public async Task AutoCheckOutByLicensePlateAsync_InvalidPlate_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AutoCheckOutByLicensePlateAsync("!!!"));
        }

        [Fact]
        public async Task AutoCheckOutByLicensePlateAsync_NoActiveSession_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.AutoCheckOutByLicensePlateAsync("51K11111"));
        }

        [Fact]
        public async Task AutoCheckOutByLicensePlateAsync_ActiveBookingUnpaid_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51K22222", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AutoCheckOutByLicensePlateAsync("51K22222"));
        }

        [Fact]
        public async Task AutoCheckOutByLicensePlateAsync_ActiveBookingPaid_CompletesBooking()
        {
            var booking = new Booking { LicensePlate = "51K33333", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "WalkInPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.AutoCheckOutByLicensePlateAsync("51K33333");

            Assert.Equal("Completed", result.Status);
        }

        [Fact]
        public async Task AutoCheckOutByLicensePlateAsync_FleetLogWithLinkedBooking_CompletesBoth()
        {
            var businessUser = new User { PhoneNumber = "0997000001", Email = "biz1@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(businessUser);
            await _dbContext.SaveChangesAsync();

            var businessProfile = new BusinessProfile
            {
                UserId = businessUser.UserId,
                CompanyName = "Fleet Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(businessProfile);

            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            var fleetVehicle = new FleetVehicle { BusinessProfileId = businessProfile.BusinessProfileId, LicensePlate = "51K44444", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Approved", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking { LicensePlate = "51K44444", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var fleetLog = new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = 1, BookingId = booking.BookingId, CheckInTime = DateTime.UtcNow.AddMinutes(-30), Status = "CheckedIn", WashCost = 0 };
            _dbContext.FleetWashLogs.Add(fleetLog);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.AutoCheckOutByLicensePlateAsync("51K44444");

            Assert.Equal("Completed", result.Status);

            var updatedLog = await _dbContext.FleetWashLogs.FirstAsync(f => f.FleetWashLogId == fleetLog.FleetWashLogId);
            Assert.Equal("Completed", updatedLog.Status);
        }

        [Fact]
        public async Task AutoCheckOutByLicensePlateAsync_FleetLogWithoutBooking_ReturnsSyntheticDTO()
        {
            var businessUser = new User { PhoneNumber = "0997000002", Email = "biz2@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(businessUser);
            await _dbContext.SaveChangesAsync();

            var businessProfile = new BusinessProfile
            {
                UserId = businessUser.UserId,
                CompanyName = "Fleet Co 2",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(businessProfile);

            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            var fleetVehicle = new FleetVehicle { BusinessProfileId = businessProfile.BusinessProfileId, LicensePlate = "51K55555", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Approved", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            var fleetLog = new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = 1, BookingId = null, CheckInTime = DateTime.UtcNow.AddMinutes(-15), Status = "Processing", WashCost = 80000 };
            _dbContext.FleetWashLogs.Add(fleetLog);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.AutoCheckOutByLicensePlateAsync("51K55555");

            Assert.Equal("Completed", result.Status);
            Assert.Equal(80000, result.FinalAmount);
        }

        [Fact]
        public async Task AutoCheckInAndStartProcessingAsync_InvalidPlate_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AutoCheckInAndStartProcessingAsync("!!!", 1, false));
        }

        [Fact]
        public async Task AutoCheckInAndStartProcessingAsync_NoBooking_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.AutoCheckInAndStartProcessingAsync("51L11111", 1, false));
        }

        [Fact]
        public async Task AutoCheckInAndStartProcessingAsync_UnpaidBooking_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51L22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AutoCheckInAndStartProcessingAsync("51L22222", 1, false));
        }

        [Fact]
        public async Task AutoCheckInAndStartProcessingAsync_PaidNoAutoStart_ChecksInOnly()
        {
            var booking = new Booking { LicensePlate = "51L33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.AutoCheckInAndStartProcessingAsync("51L33333", 1, false);

            Assert.Equal("CheckedIn", result.Status);
            Assert.Null(result.ProcessingStartTime);
        }

        [Fact]
        public async Task AutoCheckInAndStartProcessingAsync_PaidWithAutoStart_StartsProcessing()
        {
            var booking = new Booking { LicensePlate = "51L44444", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.AutoCheckInAndStartProcessingAsync("51L44444", 1, true);

            Assert.Equal("Processing", result.Status);
            Assert.NotNull(result.ProcessingStartTime);
        }
    }
}