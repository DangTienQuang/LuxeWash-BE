using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Services;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoWashPro.Tests.BLL
{
    public class BusinessBookingServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<ILaneSchedulerService> _laneSchedulerMock;
        private readonly Mock<IBookingMaterialUsageService> _materialUsageMock;
        private readonly BusinessBookingService _sut;

        public BusinessBookingServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _laneSchedulerMock = new Mock<ILaneSchedulerService>();
            _materialUsageMock = new Mock<IBookingMaterialUsageService>();
            _materialUsageMock.Setup(m => m.ConsumeForCompletedBookingAsync(It.IsAny<int>(), It.IsAny<int?>())).Returns(Task.CompletedTask);

            _sut = new BusinessBookingService(_dbContext, _laneSchedulerMock.Object, _materialUsageMock.Object);
        }

        private async Task<(User user, BusinessProfile business)> SeedApprovedBusiness()
        {
            var user = new User { PhoneNumber = "0999" + new Random().Next(100000, 999999), Email = $"biz{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var business = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Test Fleet Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(business);
            await _dbContext.SaveChangesAsync();

            return (user, business);
        }

        [Fact]
        public async Task GetActiveFleetVehiclesAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetActiveFleetVehiclesAsync(999));
        }

        [Fact]
        public async Task GetActiveFleetVehiclesAsync_ReturnsOnlyActiveVehicles()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetVehicles.AddRange(
                new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51R11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 },
                new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51R22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetActiveFleetVehiclesAsync(user.UserId);

            Assert.Single(result);
            Assert.Equal("51R11111", result[0].LicensePlate);
        }

        [Fact]
        public async Task GetBookingsAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBookingsAsync(999));
        }

        [Fact]
        public async Task GetBookingsAsync_ReturnsBookingsForBusiness()
        {
            var (user, business) = await SeedApprovedBusiness();
            _dbContext.Bookings.Add(new Booking { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51R33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBookingsAsync(user.UserId);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetBookingDetailAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var (user, business) = await SeedApprovedBusiness();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBookingDetailAsync(user.UserId, 999));
        }

        [Fact]
        public async Task GetBookingDetailAsync_ValidBooking_ReturnsDetailWithServices()
        {
            var (user, business) = await SeedApprovedBusiness();
            var service = new Service { ServiceName = "Fleet Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                BusinessProfileId = business.BusinessProfileId,
                LicensePlate = "51R44444",
                Status = "Pending",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBookingDetailAsync(user.UserId, booking.BookingId);

            Assert.Contains("Fleet Wash", result.Services);
        }

        [Fact]
        public async Task CancelBookingAsync_NotPending_ThrowsBadRequestException()
        {
            var (user, business) = await SeedApprovedBusiness();
            var booking = new Booking { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51R55555", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CancelBookingAsync(user.UserId, booking.BookingId));
        }

        [Fact]
        public async Task CancelBookingAsync_ValidBooking_CancelsAndReleasesCapacity()
        {
            var (user, business) = await SeedApprovedBusiness();
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var scheduledTime = DateTime.UtcNow.Date.AddDays(1).Add(new TimeSpan(9, 0, 0));
            var booking = new Booking { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51R66666", Status = "Pending", BranchId = 1, ScheduledTime = scheduledTime, OriginalPrice = 0, FinalAmount = 0, CapacityWeight = 5 };
            _dbContext.Bookings.Add(booking);
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = scheduledTime.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            await _sut.CancelBookingAsync(user.UserId, booking.BookingId);

            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("Cancelled", updated.Status);
            var capacity = await _dbContext.DailySlotCapacities.FirstAsync(dc => dc.SlotId == slot.SlotId);
            Assert.Equal(0, capacity.BookedWeight);
        }

        [Fact]
        public async Task CheckInAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CheckInAsync(999));
        }

        [Fact]
        public async Task CheckInAsync_NotBusinessBooking_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51R77777", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, BookingType = "Personal", OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CheckInAsync(booking.BookingId));
        }

        [Fact]
        public async Task CheckInAsync_ValidBooking_CreatesWashLogAndUpdatesStatus()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var fleetVehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51R88888", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                BusinessProfileId = business.BusinessProfileId,
                FleetVehicleId = fleetVehicle.FleetVehicleId,
                LicensePlate = "51R88888",
                Status = "Pending",
                BranchId = 1,
                BookingType = "Business",
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CheckInAsync(booking.BookingId);

            Assert.Equal("CheckedIn", result.Status);
            var updatedBooking = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("CheckedIn", updatedBooking.Status);
        }

        [Fact]
        public async Task WalkInAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var dto = new FleetWalkInDTO { LicensePlate = "NONEXISTENT", BranchId = 1 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.WalkInAsync(dto));
        }

        [Fact]
        public async Task WalkInAsync_AlreadyCheckedIn_ThrowsBadRequestException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            var fleetVehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51S11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = branch.BranchId, CheckInTime = DateTime.UtcNow, Status = "Processing", WashCost = 0 });
            await _dbContext.SaveChangesAsync();

            var dto = new FleetWalkInDTO { LicensePlate = "51S11111", BranchId = branch.BranchId };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.WalkInAsync(dto));
        }

        [Fact]
        public async Task WalkInAsync_ValidVehicle_CreatesWashLog()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            var fleetVehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51S22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            var dto = new FleetWalkInDTO { LicensePlate = "51S22222", BranchId = branch.BranchId };
            var result = await _sut.WalkInAsync(dto);

            Assert.Equal("CheckedIn", result.Status);
        }

        [Fact]
        public async Task WalkOutAsync_LogNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.WalkOutAsync(999));
        }

        [Fact]
        public async Task WalkOutAsync_NotProcessing_ThrowsBadRequestException()
        {
            var log = new FleetWashLog { FleetVehicleId = 1, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "CheckedIn", WashCost = 0 };
            _dbContext.FleetWashLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.WalkOutAsync(log.FleetWashLogId));
        }

        [Fact]
        public async Task WalkOutAsync_Processing_CompletesLog()
        {
            var log = new FleetWashLog { FleetVehicleId = 1, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Processing", WashCost = 50000 };
            _dbContext.FleetWashLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            await _sut.WalkOutAsync(log.FleetWashLogId);

            var updated = await _dbContext.FleetWashLogs.FirstAsync(f => f.FleetWashLogId == log.FleetWashLogId);
            Assert.Equal("Completed", updated.Status);
            Assert.NotNull(updated.CompletedTime);
        }

        [Fact]
        public async Task AssignLaneAsync_LogNotFound_ThrowsNotFoundException()
        {
            var dto = new AssignLaneDTO { LaneId = 1, StaffUserId = 1 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.AssignLaneAsync(999, dto));
        }

        [Fact]
        public async Task AssignLaneAsync_ValidAssignment_SetsAssignedStatus()
        {
            var log = new FleetWashLog { FleetVehicleId = 1, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "CheckedIn", WashCost = 0 };
            _dbContext.FleetWashLogs.Add(log);

            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);

            var staff = new User { PhoneNumber = "0999888777", Email = "staff@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staff);
            await _dbContext.SaveChangesAsync();

            var dto = new AssignLaneDTO { LaneId = lane.LaneId, StaffUserId = staff.UserId };
            await _sut.AssignLaneAsync(log.FleetWashLogId, dto);

            var updated = await _dbContext.FleetWashLogs.FirstAsync(f => f.FleetWashLogId == log.FleetWashLogId);
            Assert.Equal("Assigned", updated.Status);
        }

        [Fact]
        public async Task CheckOutAsync_NotProcessing_ThrowsBadRequestException()
        {
            var log = new FleetWashLog { FleetVehicleId = 1, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "CheckedIn", WashCost = 0 };
            _dbContext.FleetWashLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CheckOutAsync(log.FleetWashLogId));
        }

        [Fact]
        public async Task CheckOutAsync_ValidLog_CompletesLogAndConsumesMaterial()
        {
            var booking = new Booking { LicensePlate = "51S33333", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var log = new FleetWashLog { FleetVehicleId = 1, BranchId = 1, BookingId = booking.BookingId, CheckInTime = DateTime.UtcNow, Status = "Processing", WashCost = 100000 };
            _dbContext.FleetWashLogs.Add(log);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CheckOutAsync(log.FleetWashLogId);

            Assert.NotNull(result.CompletedTime);
            var updatedBooking = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("Completed", updatedBooking.Status);
            _materialUsageMock.Verify(m => m.ConsumeForCompletedBookingAsync(booking.BookingId, It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task GetCurrentVehiclesAsync_ReturnsOnlyActiveLogs()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var fleetVehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.AddRange(
                new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "CheckedIn", WashCost = 0 },
                new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Completed", WashCost = 0 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetCurrentVehiclesAsync();

            Assert.Single(result);
        }

        [Fact]
        public async Task GetInvoiceByBookingAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetInvoiceByBookingAsync(999));
        }

        [Fact]
        public async Task GetInvoiceByBookingAsync_Found_ReturnsDTO()
        {
            var invoice = new Invoice { BookingId = 5, InvoiceCode = "INV001", Subtotal = 100000, TaxAmount = 10000, TotalAmount = 110000, Status = "Paid", IssuedAt = DateTime.UtcNow };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetInvoiceByBookingAsync(5);

            Assert.Equal("INV001", result.InvoiceCode);
        }

        [Fact]
        public async Task GetFleetWashHistoryAsync_NoBusiness_ThrowsNotFoundException()
        {
            var filter = new FleetHistoryFilterDTO { Page = 1, PageSize = 10 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetFleetWashHistoryAsync(999, filter));
        }

        [Fact]
        public async Task GetFleetWashHistoryAsync_FiltersByVehicleId()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var v1 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            var v2 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T33333", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.AddRange(v1, v2);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.AddRange(
                new FleetWashLog { FleetVehicleId = v1.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Completed", WashCost = 50000, CompletedTime = DateTime.UtcNow },
                new FleetWashLog { FleetVehicleId = v2.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Completed", WashCost = 60000, CompletedTime = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var filter = new FleetHistoryFilterDTO { Page = 1, PageSize = 10, FleetVehicleId = v1.FleetVehicleId };
            var result = await _sut.GetFleetWashHistoryAsync(user.UserId, filter);

            Assert.Single(result);
            Assert.Equal("51T22222", result[0].LicensePlate);
        }

        [Fact]
        public async Task GetDashboardAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetDashboardAsync(999));
        }

        [Fact]
        public async Task GetDashboardAsync_CountsCorrectly()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var v1 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T44444", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            var v2 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T55555", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.AddRange(v1, v2);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = v1.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.Today, Status = "Completed", WashCost = 50000 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetDashboardAsync(user.UserId);

            Assert.Equal(2, result.TotalVehicles);
            Assert.Equal(1, result.ActiveVehicles);
            Assert.Equal(1, result.PendingVehicles);
            Assert.Equal(1, result.TodayWashCount);
            Assert.Equal(50000, result.MonthlySpend);
        }

        [Fact]
        public async Task GetInvoicesAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetInvoicesAsync(999));
        }

        [Fact]
        public async Task GetInvoicesAsync_ReturnsInvoicesForBusiness()
        {
            var (user, business) = await SeedApprovedBusiness();
            var booking = new Booking { LicensePlate = "51T66666", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Invoices.Add(new Invoice { BusinessProfileId = business.BusinessProfileId, BookingId = booking.BookingId, InvoiceCode = "INV002", Subtotal = 0, TaxAmount = 0, TotalAmount = 0, Status = "Paid", IssuedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetInvoicesAsync(user.UserId);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetInvoiceDetailAsync_NotFound_ThrowsNotFoundException()
        {
            var (user, business) = await SeedApprovedBusiness();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetInvoiceDetailAsync(user.UserId, 999));
        }

        [Fact]
        public async Task GetMonthlyStatementAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetMonthlyStatementAsync(999, 2026, 7));
        }

        [Fact]
        public async Task GetMonthlyStatementAsync_AggregatesByVehicle()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T77777", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var july2026 = new DateTime(2026, 7, 15);
            _dbContext.FleetWashLogs.AddRange(
                new FleetWashLog { FleetVehicleId = vehicle.FleetVehicleId, BranchId = 1, CheckInTime = july2026, Status = "Completed", WashCost = 50000 },
                new FleetWashLog { FleetVehicleId = vehicle.FleetVehicleId, BranchId = 1, CheckInTime = july2026.AddDays(1), Status = "Completed", WashCost = 60000 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetMonthlyStatementAsync(user.UserId, 2026, 7);

            Assert.Equal(2, result.TotalWashes);
            Assert.Equal(110000, result.TotalCost);
            Assert.Single(result.Vehicles);
            Assert.Equal(110000, result.Vehicles[0].TotalCost);
        }

        [Fact]
        public async Task GetActiveVehiclesOnFloorAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetActiveVehiclesOnFloorAsync(999));
        }

        [Fact]
        public async Task GetActiveVehiclesOnFloorAsync_CombinesBookingsAndWashLogs()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T88888", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            _dbContext.Bookings.Add(new Booking { BusinessProfileId = business.BusinessProfileId, FleetVehicleId = vehicle.FleetVehicleId, LicensePlate = "51T88888", Status = "Pending", BookingType = "Business", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow.AddHours(2), OriginalPrice = 0, FinalAmount = 0 });
            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = vehicle.FleetVehicleId, BranchId = branch.BranchId, CheckInTime = DateTime.UtcNow, Status = "CheckedIn", WashCost = 0 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetActiveVehiclesOnFloorAsync(user.UserId);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetVehiclesByStatusAsync_NoBusiness_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetVehiclesByStatusAsync(999, null));
        }

        [Fact]
        public async Task GetVehiclesByStatusAsync_FilterByProcessing_ReturnsOnlyMatchingLogs()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51T99999", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.AddRange(
                new FleetWashLog { FleetVehicleId = vehicle.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Processing", WashCost = 0 },
                new FleetWashLog { FleetVehicleId = vehicle.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Completed", WashCost = 0 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetVehiclesByStatusAsync(user.UserId, "Processing");

            Assert.Single(result);
            Assert.Equal("Processing", result[0].Status);
        }

        [Fact]
        public async Task GetAvailableSlotsForBusinessAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            var request = new CheckBusinessSlotsRequestDTO { BranchId = 1, FleetVehicleId = 1, TargetDate = DateTime.UtcNow.AddDays(1), VehicleCount = 1, ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAvailableSlotsForBusinessAsync(999, request));
        }

        [Fact]
        public async Task GetAvailableSlotsForBusinessAsync_VehicleNotFound_ThrowsNotFoundException()
        {
            var (user, business) = await SeedApprovedBusiness();
            var request = new CheckBusinessSlotsRequestDTO { BranchId = 1, FleetVehicleId = 999, TargetDate = DateTime.UtcNow.AddDays(1), VehicleCount = 1, ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAvailableSlotsForBusinessAsync(user.UserId, request));
        }

        [Fact]
        public async Task GetAvailableSlotsForBusinessAsync_PastDate_ThrowsBadRequestException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var request = new CheckBusinessSlotsRequestDTO { BranchId = 1, FleetVehicleId = vehicle.FleetVehicleId, TargetDate = DateTime.UtcNow.AddDays(-2), VehicleCount = 1, ServiceIds = new List<int>() };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetAvailableSlotsForBusinessAsync(user.UserId, request));
        }

        [Fact]
        public async Task GetAvailableSlotsForBusinessAsync_ServicesNotPriced_ThrowsBadRequestException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var request = new CheckBusinessSlotsRequestDTO { BranchId = 1, FleetVehicleId = vehicle.FleetVehicleId, TargetDate = DateTime.UtcNow.AddDays(1), VehicleCount = 1, ServiceIds = new List<int> { 999 } };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetAvailableSlotsForBusinessAsync(user.UserId, request));
        }

        [Fact]
        public async Task GetAvailableSlotsForBusinessAsync_ScheduleFails_MarksSlotUnavailable()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U33333", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            _laneSchedulerMock.Setup(l => l.ScheduleFleetAsync(1, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Fail("No lanes available"));

            var request = new CheckBusinessSlotsRequestDTO { BranchId = 1, FleetVehicleId = vehicle.FleetVehicleId, TargetDate = DateTime.UtcNow.AddDays(1), VehicleCount = 1, ServiceIds = new List<int>() };
            var result = await _sut.GetAvailableSlotsForBusinessAsync(user.UserId, request);

            Assert.False(result[0].IsAvailable);
            Assert.Equal("No lanes available", result[0].Reason);
        }

        [Fact]
        public async Task GetAvailableSlotsForBusinessAsync_ScheduleSucceeds_PopulatesProjections()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U44444", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            var lane = new Lane { BranchId = 1, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.Add(new TimeSpan(9, 0, 0));
            _laneSchedulerMock.Setup(l => l.ScheduleFleetAsync(1, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Ok(new List<VehicleAssignment>
                {
            new VehicleAssignment { FleetVehicleId = 0, LaneId = lane.LaneId, EstimatedStart = slotStart, EstimatedEnd = slotStart.AddMinutes(30) }
                }));

            var request = new CheckBusinessSlotsRequestDTO { BranchId = 1, FleetVehicleId = vehicle.FleetVehicleId, TargetDate = DateTime.UtcNow.AddDays(1), VehicleCount = 1, ServiceIds = new List<int>() };
            var result = await _sut.GetAvailableSlotsForBusinessAsync(user.UserId, request);

            Assert.True(result[0].IsAvailable);
            Assert.Single(result[0].VehicleProjections);
            Assert.Equal("Lane 1", result[0].VehicleProjections[0].LaneName);
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            var dto = new CreateBusinessBookingDTO { BranchId = 1, SlotId = 1, ScheduledTime = DateTime.UtcNow.AddDays(1), Vehicles = new List<VehicleBookingItemDTO>() };
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateBusinessBookingAsync(999, dto));
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_VehicleInactive_ThrowsBadRequestException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U55555", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateBusinessBookingDTO
            {
                BranchId = 1,
                SlotId = 1,
                ScheduledTime = DateTime.UtcNow.AddDays(1),
                Vehicles = new List<VehicleBookingItemDTO> { new VehicleBookingItemDTO { FleetVehicleId = vehicle.FleetVehicleId, ServiceIds = new List<int> { 1 } } }
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBusinessBookingAsync(user.UserId, dto));
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U66666", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateBusinessBookingDTO
            {
                BranchId = branch.BranchId,
                SlotId = 999,
                ScheduledTime = DateTime.UtcNow.AddDays(1),
                Vehicles = new List<VehicleBookingItemDTO> { new VehicleBookingItemDTO { FleetVehicleId = vehicle.FleetVehicleId, ServiceIds = new List<int> { 1 } } }
            };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateBusinessBookingAsync(user.UserId, dto));
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_ScheduleFails_ThrowsBadRequestException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U77777", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            var slot = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = branch.BranchId, Price = 100000, CapacityWeight = 5 });
            await _dbContext.SaveChangesAsync();

            _laneSchedulerMock.Setup(l => l.ScheduleFleetAsync(branch.BranchId, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Fail("All lanes busy"));

            var dto = new CreateBusinessBookingDTO
            {
                BranchId = branch.BranchId,
                SlotId = slot.SlotId,
                ScheduledTime = DateTime.UtcNow.AddDays(1),
                Vehicles = new List<VehicleBookingItemDTO> { new VehicleBookingItemDTO { FleetVehicleId = vehicle.FleetVehicleId, ServiceIds = new List<int> { service.ServiceId } } }
            };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateBusinessBookingAsync(user.UserId, dto));
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_ValidSingleVehicle_CreatesBooking()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var branch = new Branch { Name = "Main", IsActive = true };
            _dbContext.Branches.Add(branch);
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51U88888", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            var slot = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane A" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = service.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = branch.BranchId, Price = 150000, CapacityWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var scheduledDate = DateTime.UtcNow.AddDays(1);
            var scheduledTime = scheduledDate.Date.Add(slot.StartTime);

            _laneSchedulerMock.Setup(l => l.ScheduleFleetAsync(branch.BranchId, scheduledTime, It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Ok(new List<VehicleAssignment>
                {
            new VehicleAssignment { FleetVehicleId = vehicle.FleetVehicleId, LaneId = lane.LaneId, EstimatedStart = scheduledTime, EstimatedEnd = scheduledTime.AddMinutes(30) }
                }));

            var dto = new CreateBusinessBookingDTO
            {
                BranchId = branch.BranchId,
                SlotId = slot.SlotId,
                ScheduledTime = scheduledDate,
                Vehicles = new List<VehicleBookingItemDTO> { new VehicleBookingItemDTO { FleetVehicleId = vehicle.FleetVehicleId, ServiceIds = new List<int> { service.ServiceId } } }
            };

            var result = await _sut.CreateBusinessBookingAsync(user.UserId, dto);

            Assert.Equal(1, result.TotalVehicles);
            Assert.Equal(150000, result.TotalAmount);
            Assert.Single(result.Vehicles);
        }

        [Fact]
        public async Task RescheduleBookingAsync_ScheduleFails_ThrowsBadRequestException()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51V22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            var newSlot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(newSlot);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                BusinessProfileId = business.BusinessProfileId,
                FleetVehicleId = vehicle.FleetVehicleId,
                LicensePlate = "51V22222",
                Status = "Pending",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.AddDays(3),
                OriginalPrice = 0,
                FinalAmount = 0
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _laneSchedulerMock.Setup(l => l.ScheduleFleetAsync(1, It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Fail("No lanes free at new time"));

            var dto = new RescheduleBusinessBookingDTO { BookingId = booking.BookingId, NewSlotId = newSlot.SlotId, NewScheduledDate = DateTime.UtcNow.AddDays(5) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RescheduleBookingAsync(user.UserId, dto));
        }

        [Fact]
        public async Task RescheduleBookingAsync_Valid_UpdatesScheduleAndCapacity()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var (user, business) = await SeedApprovedBusiness();
            var vehicle = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51V33333", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            var newSlot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(newSlot);
            var lane = new Lane { BranchId = 1, Name = "Lane B" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                BusinessProfileId = business.BusinessProfileId,
                FleetVehicleId = vehicle.FleetVehicleId,
                LicensePlate = "51V33333",
                Status = "Pending",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.AddDays(3),
                OriginalPrice = 0,
                FinalAmount = 0,
                CapacityWeight = 5
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var newScheduledDate = DateTime.UtcNow.AddDays(5);
            var newScheduledTime = newScheduledDate.Date.Add(newSlot.StartTime);

            _laneSchedulerMock.Setup(l => l.ScheduleFleetAsync(1, newScheduledTime, It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Ok(new List<VehicleAssignment>
                {
            new VehicleAssignment { FleetVehicleId = vehicle.FleetVehicleId, LaneId = lane.LaneId, EstimatedStart = newScheduledTime, EstimatedEnd = newScheduledTime.AddMinutes(30) }
                }));

            var dto = new RescheduleBusinessBookingDTO { BookingId = booking.BookingId, NewSlotId = newSlot.SlotId, NewScheduledDate = newScheduledDate };
            var result = await _sut.RescheduleBookingAsync(user.UserId, dto);

            Assert.Equal(newScheduledTime, result.NewScheduledTime);
            Assert.Equal("Lane B", result.LaneName);
        }
    }
}
