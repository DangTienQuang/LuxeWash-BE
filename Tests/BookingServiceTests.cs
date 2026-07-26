using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.Tests
{
    public class BookingServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly Mock<IWalletService> _mockWalletService;
        private readonly Mock<ITierService> _mockTierService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IVoucherService> _mockVoucherService;
        private readonly Mock<IVoucherCampaignService> _mockVoucherCampaignService;
        private readonly Mock<IPayOsService> _mockPayOsService;
        private readonly Mock<IBookingMaterialUsageService> _mockBookingMaterialUsageService;
        private readonly Mock<IOccupancyService> _mockOccupancyService;
        private readonly BookingService _bookingService;

        public BookingServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AutoWashDbContext(options);

            _mockWalletService = new Mock<IWalletService>(MockBehavior.Default);
            _mockTierService = new Mock<ITierService>(MockBehavior.Default);
            _mockEmailService = new Mock<IEmailService>(MockBehavior.Default);
            _mockVoucherService = new Mock<IVoucherService>(MockBehavior.Default);
            _mockVoucherCampaignService = new Mock<IVoucherCampaignService>(MockBehavior.Default);
            _mockPayOsService = new Mock<IPayOsService>(MockBehavior.Default);
            _mockBookingMaterialUsageService = new Mock<IBookingMaterialUsageService>(MockBehavior.Default);
            _mockOccupancyService = new Mock<IOccupancyService>(MockBehavior.Default);

            _bookingService = new BookingService(
                _context,
                _mockWalletService.Object,
                _mockTierService.Object,
                _mockEmailService.Object,
                _mockVoucherService.Object,
                _mockVoucherCampaignService.Object,
                _mockPayOsService.Object,
                _mockBookingMaterialUsageService.Object,
                _mockOccupancyService.Object
            );
        }

        [Fact]
        public async Task CreateBookingAsync_ValidRequest_CreatesBookingSuccessfully_TC01()
        {
            // Arrange
            int userId = 1;
            int branchId = 1;
            int vehicleTypeId = 1;
            int vehicleId = 1;
            int serviceId = 1;
            int slotId = 1;
            string licensePlate = "51G-12345";
            var scheduledDate = DateTime.UtcNow.Date.AddDays(1);

            // Seed User
            _context.Users.Add(new User
            {
                UserId = userId,
                PhoneNumber = "0123456789",
                PasswordHash = "hash",
                Role = "Customer",
                Status = "Active"
            });

            // Seed CustomerProfile
            _context.CustomerProfiles.Add(new CustomerProfile
            {
                UserId = userId,
                TotalPoint = 0,
                FullName = "Test User"
            });

            // Seed Wallet
            _context.Wallets.Add(new Wallet
            {
                UserId = userId,
                Balance = 500000,
                Status = "Active"
            });

            // Seed Branch
            _context.Branches.Add(new Branch
            {
                BranchId = branchId,
                Name = "Test Branch",
                Address = "Test Address",
                IsActive = true
            });

            // Seed VehicleType
            _context.VehicleTypes.Add(new VehicleType
            {
                Id = vehicleTypeId,
                Name = "Test Type",
                BaseWeight = 1
            });

            // Seed Vehicle
            _context.Vehicles.Add(new Vehicle
            {
                Id = vehicleId,
                UserId = userId,
                LicensePlate = licensePlate,
                VehicleTypeId = vehicleTypeId,
                IsDeleted = false
            });

            // Seed Service
            _context.Services.Add(new Service
            {
                ServiceId = serviceId,
                ServiceName = "Car Wash",
                IsActive = true
            });

            // Seed ServicePrice
            _context.ServicePrices.Add(new ServicePrice
            {
                ServicePriceId = 1,
                ServiceId = serviceId,
                VehicleTypeId = vehicleTypeId,
                BranchId = branchId,
                Price = 100000,
                CapacityWeight = 1
            });

            // Seed TimeSlot
            _context.TimeSlots.Add(new TimeSlot
            {
                SlotId = slotId,
                BranchId = branchId,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(9, 0, 0),
                MaxCapacity = 3,
                IsVipOnly = false
            });

            await _context.SaveChangesAsync();

            var request = new CreateBookingDTO
            {
                BranchId = branchId,
                VehicleId = vehicleId,
                LicensePlate = licensePlate,
                ServiceIds = new List<int> { serviceId },
                ScheduledDate = scheduledDate,
                SlotId = slotId,
                PaymentMethod = "Wallet",
                PointsToUse = 0,
                VoucherId = null
            };

            // Act
            var result = await _bookingService.CreateBookingAsync(userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Pending", result.Status);

            var bookingsInDb = await _context.Bookings.ToListAsync();
            Assert.Single(bookingsInDb);
            Assert.Equal(userId, bookingsInDb.First().UserId);
            Assert.Equal(licensePlate, bookingsInDb.First().LicensePlate);
            Assert.Equal(branchId, bookingsInDb.First().BranchId);
            Assert.Equal(100000, bookingsInDb.First().OriginalPrice);
        }

        private async Task SeedBaseDataAsync(int userId, int branchId, int vehicleTypeId, int vehicleId, int serviceId, int slotId, string licensePlate)
        {
            _context.Users.Add(new User { UserId = userId, PhoneNumber = "0123456789", PasswordHash = "hash", Role = "Customer", Status = "Active" });
            _context.CustomerProfiles.Add(new CustomerProfile { UserId = userId, TotalPoint = 100, FullName = "Test User" });
            _context.Wallets.Add(new Wallet { UserId = userId, Balance = 500000, Status = "Active" });
            _context.Branches.Add(new Branch { BranchId = branchId, Name = "Test Branch", Address = "Test Address", IsActive = true });
            _context.VehicleTypes.Add(new VehicleType { Id = vehicleTypeId, Name = "Test Type", BaseWeight = 1 });
            _context.Vehicles.Add(new Vehicle { Id = vehicleId, UserId = userId, LicensePlate = licensePlate, VehicleTypeId = vehicleTypeId, IsDeleted = false });
            _context.Services.Add(new Service { ServiceId = serviceId, ServiceName = "Car Wash", IsActive = true });
            _context.ServicePrices.Add(new ServicePrice { ServicePriceId = userId * 1000, ServiceId = serviceId, VehicleTypeId = vehicleTypeId, BranchId = branchId, Price = 100000, CapacityWeight = 1 });
            _context.TimeSlots.Add(new TimeSlot { SlotId = slotId, BranchId = branchId, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 0, 0), MaxCapacity = 3, IsVipOnly = false });
            
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateBookingAsync_SlotFullyBooked_ThrowsException_TC02()
        {
            // Arrange
            int userId = 2, branchId = 2, vehicleTypeId = 2, vehicleId = 2, serviceId = 2, slotId = 2;
            string licensePlate = "51G-22222";
            var scheduledDate = DateTime.UtcNow.Date.AddDays(1);
            await SeedBaseDataAsync(userId, branchId, vehicleTypeId, vehicleId, serviceId, slotId, licensePlate);

            _context.DailySlotCapacities.Add(new DailySlotCapacity 
            { 
                BranchId = branchId, 
                Date = scheduledDate, 
                SlotId = slotId, 
                BookedWeight = 3 
            });
            await _context.SaveChangesAsync();

            var request = new CreateBookingDTO
            {
                BranchId = branchId, VehicleId = vehicleId, LicensePlate = licensePlate,
                ServiceIds = new List<int> { serviceId }, ScheduledDate = scheduledDate, SlotId = slotId,
                PaymentMethod = "Wallet"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.CreateBookingAsync(userId, request));
            Assert.Contains("Insufficient shop capacity", exception.Message);
        }

        [Fact]
        public async Task CreateBookingAsync_InvalidVoucherBranch_ThrowsException_TC03()
        {
            // Arrange
            int userId = 3, branchId = 3, vehicleTypeId = 3, vehicleId = 3, serviceId = 3, slotId = 3;
            string licensePlate = "51G-33333";
            await SeedBaseDataAsync(userId, branchId, vehicleTypeId, vehicleId, serviceId, slotId, licensePlate);

            var request = new CreateBookingDTO
            {
                BranchId = branchId, VehicleId = vehicleId, LicensePlate = licensePlate,
                ServiceIds = new List<int> { serviceId }, ScheduledDate = DateTime.UtcNow.Date.AddDays(1), SlotId = slotId,
                PaymentMethod = "Wallet", VoucherId = 999 // This voucher ID does not exist in DB
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.NotFoundException>(() => _bookingService.CreateBookingAsync(userId, request));
            Assert.Contains("You do not own this voucher", exception.Message);
        }

        [Fact]
        public async Task CreateBookingAsync_InsufficientPoints_ThrowsException_TC04()
        {
            // Arrange
            int userId = 4, branchId = 4, vehicleTypeId = 4, vehicleId = 4, serviceId = 4, slotId = 4;
            string licensePlate = "51G-44444";
            await SeedBaseDataAsync(userId, branchId, vehicleTypeId, vehicleId, serviceId, slotId, licensePlate);

            // Set points to 0 to trigger the "Not enough points" exception when requesting to use points
            var profile = await _context.CustomerProfiles.FirstAsync(p => p.UserId == userId);
            profile.TotalPoint = 0;
            await _context.SaveChangesAsync();

            var request = new CreateBookingDTO
            {
                BranchId = branchId, VehicleId = vehicleId, LicensePlate = licensePlate,
                ServiceIds = new List<int> { serviceId }, ScheduledDate = DateTime.UtcNow.Date.AddDays(1), SlotId = slotId,
                PaymentMethod = "Wallet", PointsToUse = 500
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.CreateBookingAsync(userId, request));
            Assert.Contains("Not enough points", exception.Message);
        }

        [Fact]
        public async Task CreateBookingAsync_WalletPaymentInsufficientBalance_ThrowsException()
        {
            // Arrange
            int userId = 5, branchId = 5, vehicleTypeId = 5, vehicleId = 5, serviceId = 5, slotId = 5;
            string licensePlate = "51G-55555";
            await SeedBaseDataAsync(userId, branchId, vehicleTypeId, vehicleId, serviceId, slotId, licensePlate);
            
            var wallet = await _context.Wallets.FirstAsync(w => w.UserId == userId);
            wallet.Balance = 50000; // Price is 100,000
            await _context.SaveChangesAsync();

            var request = new CreateBookingDTO
            {
                BranchId = branchId, VehicleId = vehicleId, LicensePlate = licensePlate,
                ServiceIds = new List<int> { serviceId }, ScheduledDate = DateTime.UtcNow.Date.AddDays(1), SlotId = slotId,
                PaymentMethod = "Wallet"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.CreateBookingAsync(userId, request));
            Assert.Contains("Insufficient wallet balance for deposit", exception.Message);
        }

        [Fact]
        public async Task CheckCompatibilityAsync_ValidCombination_ReturnsTrue_TC05()
        {
            // Arrange
            int userId = 10;
            int branchId = 10;
            int slotId = 10;
            var targetDate = DateTime.UtcNow.Date.AddDays(1);
            int vehicleTypeId = 10;
            int vehicleId = 10;
            int serviceId = 10;

            _context.Users.Add(new User { UserId = userId, PhoneNumber = "0901111111", PasswordHash = "hash", Role = "Customer", Status = "Active" });
            _context.CustomerProfiles.Add(new CustomerProfile { ProfileId = userId, UserId = userId, FullName = "Test", TotalPoint = 0 });
            _context.Branches.Add(new Branch { BranchId = branchId, Name = "Branch 10", IsActive = true });
            _context.TimeSlots.Add(new TimeSlot { SlotId = slotId, BranchId = branchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0) });
            
            _context.VehicleTypes.Add(new VehicleType { Id = vehicleTypeId, Name = "Sedan", BaseWeight = 1 });
            _context.Vehicles.Add(new Vehicle { Id = vehicleId, UserId = userId, VehicleTypeId = vehicleTypeId, LicensePlate = "51A-11111" });
            _context.Services.Add(new Service { ServiceId = serviceId, ServiceName = "Sedan Wash", IsActive = true });
            
            _context.ServicePrices.Add(new ServicePrice { ServiceId = serviceId, VehicleTypeId = vehicleTypeId, BranchId = branchId, Price = 100000, CapacityWeight = 1 });
            
            await _context.SaveChangesAsync();

            var request = new CheckCompatibilityRequestDTO
            {
                BranchId = branchId,
                SlotId = slotId,
                TargetDate = targetDate,
                LicensePlate = "51A-11111",
                ServiceIds = new List<int> { serviceId },
                VehicleId = vehicleId
            };

            // Act
            var result = await _bookingService.CheckCompatibilityAsync(userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsCompatible);
        }

        [Fact]
        public async Task CheckCompatibilityAsync_IncompatibleCombination_ReturnsFalse_TC06()
        {
            // Arrange
            int userId = 11;
            int branchId = 11;
            int slotId = 11;
            var targetDate = DateTime.UtcNow.Date.AddDays(1);
            int vehicleTypeId = 11; // Sedan
            int vehicleId = 11;
            int serviceId = 11; // SUV Wash

            _context.Users.Add(new User { UserId = userId, PhoneNumber = "0902222222", PasswordHash = "hash", Role = "Customer", Status = "Active" });
            _context.CustomerProfiles.Add(new CustomerProfile { ProfileId = userId, UserId = userId, FullName = "Test", TotalPoint = 0 });
            _context.Branches.Add(new Branch { BranchId = branchId, Name = "Branch 11", IsActive = true });
            _context.TimeSlots.Add(new TimeSlot { SlotId = slotId, BranchId = branchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0) });
            
            _context.VehicleTypes.Add(new VehicleType { Id = vehicleTypeId, Name = "Sedan", BaseWeight = 1 });
            _context.Vehicles.Add(new Vehicle { Id = vehicleId, UserId = userId, VehicleTypeId = vehicleTypeId, LicensePlate = "51A-22222" });
            _context.Services.Add(new Service { ServiceId = serviceId, ServiceName = "SUV Wash", IsActive = true });
            
            // Notice: We do NOT add a ServicePrice link here to simulate incompatibility.
            
            await _context.SaveChangesAsync();

            var request = new CheckCompatibilityRequestDTO
            {
                BranchId = branchId,
                SlotId = slotId,
                TargetDate = targetDate,
                LicensePlate = "51A-22222",
                ServiceIds = new List<int> { serviceId },
                VehicleId = vehicleId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.CheckCompatibilityAsync(userId, request));
            Assert.Contains("not supported for this vehicle type", ex.Message);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_PastDate_ThrowsException_TC07()
        {
            _context.Tiers.Add(new Tier { TierId = 1, TierName = "Standard", BookingWindowDays = 7, MinAccumulatedPoints = 0 });
            _context.CustomerProfiles.Add(new CustomerProfile { ProfileId = 14, UserId = 1, TierId = 1, FullName = "Test" });
            await _context.SaveChangesAsync();
            
            var request = new CheckAvailableSlotsRequestDTO
            {
                BranchId = 1,
                TargetDate = DateTime.UtcNow.Date.AddDays(-1), // Past Date
                VehicleTypeId = 1,
                ServiceIds = new List<int> { 1 }
            };

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.GetAvailableSlotsAsync(1, request));
            Assert.Contains("tier standard can only book", ex.Message.ToLower());
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_FarFuture_ThrowsException_TC08()
        {
            _context.Tiers.Add(new Tier { TierId = 2, TierName = "Standard", BookingWindowDays = 7, MinAccumulatedPoints = 0 });
            _context.CustomerProfiles.Add(new CustomerProfile { ProfileId = 15, UserId = 2, TierId = 2, FullName = "Test" });
            await _context.SaveChangesAsync();
            
            var request = new CheckAvailableSlotsRequestDTO
            {
                BranchId = 1,
                TargetDate = DateTime.UtcNow.Date.AddDays(31), // > 30 days
                VehicleTypeId = 1,
                ServiceIds = new List<int> { 1 }
            };

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.GetAvailableSlotsAsync(2, request));
            Assert.Contains("tier standard can only book", ex.Message.ToLower());
        }

        [Fact]
        public async Task CancelBookingAsync_EarlyCancel_Success_TC09()
        {
            // Arrange
            int userId = 12;
            int bookingId = 12;
            _context.Users.Add(new User { UserId = userId, PhoneNumber = "0903333333", PasswordHash = "hash", Role = "Customer", Status = "Active" });
            _context.CustomerProfiles.Add(new CustomerProfile { ProfileId = userId, UserId = userId, FullName = "Test", TotalPoint = 0 });
            
            _context.Bookings.Add(new Booking
            {
                BookingId = bookingId,
                UserId = userId,
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.Date.AddDays(1).AddHours(12), // Tomorrow at 12:00 PM
                Status = "Pending",
                LicensePlate = "51A-33333",
                OriginalPrice = 100000,
                FinalAmount = 100000
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _bookingService.CancelBookingAsync(userId, bookingId);

            // Assert
            Assert.True(result);
            var booking = await _context.Bookings.FindAsync(bookingId);
            Assert.Equal("Cancelled", booking.Status);
        }

        [Fact]
        public async Task CancelBookingAsync_ProcessingBooking_ThrowsException_TC10()
        {
            // Arrange
            int userId = 13;
            int bookingId = 13;
            _context.Users.Add(new User { UserId = userId, PhoneNumber = "0904444444", PasswordHash = "hash", Role = "Customer", Status = "Active" });
            _context.CustomerProfiles.Add(new CustomerProfile { ProfileId = userId, UserId = userId, FullName = "Test", TotalPoint = 0 });
            
            _context.Bookings.Add(new Booking
            {
                BookingId = bookingId,
                UserId = userId,
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow,
                Status = "Processing", // Already washing
                LicensePlate = "51A-44444",
                OriginalPrice = 100000,
                FinalAmount = 100000
            });
            await _context.SaveChangesAsync();

            // Act
            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.CancelBookingAsync(userId, bookingId));
            Assert.Contains("cancel", ex.Message.ToLower());
        }

        // ---------------------------------------------------------
        // Phase 3: Staff Operations Booking Methods
        // ---------------------------------------------------------

        [Fact]
        public async Task MarkAsNoShowAsync_Valid_UpdatesStatus_TC18()
        {
            int bookingId = 20;
            _context.Bookings.Add(new Booking { BookingId = bookingId, ScheduledTime = DateTime.UtcNow.AddHours(-1), Status = "CheckedIn", LicensePlate = "123" });
            await _context.SaveChangesAsync();

            await _bookingService.MarkAsNoShowAsync(bookingId);
            var booking = await _context.Bookings.FindAsync(bookingId);
            Assert.Equal("NoShow", booking.Status);
        }

        [Fact]
        public async Task MarkAsNoShowAsync_Premature_ThrowsException_TC19()
        {
            int bookingId = 21;
            // Only 5 minutes late (grace period is 30)
            _context.Bookings.Add(new Booking { BookingId = bookingId, ScheduledTime = DateTime.UtcNow.AddMinutes(-5), Status = "CheckedIn", LicensePlate = "123" });
            await _context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => _bookingService.MarkAsNoShowAsync(bookingId));
            Assert.Contains("grace period", ex.Message);
        }

        [Fact]
        public async Task ReportMismatchAsync_Valid_UpdatesPrice_TC21()
        {
            int bookingId = 22;
            _context.Bookings.Add(new Booking 
            { 
                BookingId = bookingId, 
                Status = "CheckedIn", 
                BranchId = 1,
                LicensePlate = "123",
                BookingDetails = new List<BookingDetail> 
                { 
                    new BookingDetail { ServiceId = 1, Price = 100000 } 
                } 
            });
            _context.ServicePrices.Add(new ServicePrice { ServiceId = 1, VehicleTypeId = 2, BranchId = 1, Price = 150000, CapacityWeight = 1 });
            await _context.SaveChangesAsync();

            await _bookingService.ReportMismatchAsync(bookingId, AutoWashPro.BLL.Enums.VehicleConditionEnum.Clean, 2);

            var booking = await _context.Bookings.FindAsync(bookingId);
            Assert.Equal(50000, booking.MismatchSurcharge);
            Assert.Equal(2, booking.ActualVehicleTypeId);
        }

        [Fact]
        public async Task ForceCancelBookingsAsync_Valid_CancelsAndRefunds_TC23()
        {
            int bookingId = 23;
            _context.Bookings.Add(new Booking 
            { 
                BookingId = bookingId, 
                UserId = 1,
                BranchId = 1,
                Status = "CheckedIn", 
                ScheduledTime = DateTime.UtcNow.Date,
                LicensePlate = "123",
                FinalAmount = 100000
            });
            _context.Transactions.Add(new Transaction { TransactionId = 1, ReferenceBookingId = bookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "Test" });
            await _context.SaveChangesAsync();

            var req = new ForceCancelRequestDTO { BranchId = 1, AffectedDate = DateTime.UtcNow.Date, Reason = "Broken" };
            await _bookingService.ForceCancelBookingsAsync(req);

            var booking = await _context.Bookings.FindAsync(bookingId);
            Assert.Equal("CancelledBySystem", booking.Status);
            _mockWalletService.Verify(w => w.RefundBalanceAsync(1, 100000, It.IsAny<string>()), Times.Once);
        }
    }
}
