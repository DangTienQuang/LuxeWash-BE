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
using BLL.Helpers;

namespace AutoWashPro.Tests.BLL
{
    public class OperationStaffServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IWalletService> _walletMock;
        private readonly Mock<IBookingMaterialUsageService> _materialUsageMock;
        private readonly OperationStaffService _sut;

        public OperationStaffServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _walletMock = new Mock<IWalletService>();
            _materialUsageMock = new Mock<IBookingMaterialUsageService>();
            _materialUsageMock.Setup(m => m.ConsumeForCompletedBookingAsync(It.IsAny<int>(), It.IsAny<int?>())).Returns(Task.CompletedTask);

            _sut = new OperationStaffService(_dbContext, _walletMock.Object, _materialUsageMock.Object);
        }

        [Fact]
        public async Task GetTodayLaneAssignmentAsync_NoAssignment_ReturnsDefaultAllLanes()
        {
            var result = await _sut.GetTodayLaneAssignmentAsync(1);

            Assert.Equal(0, result.LaneId);
            Assert.Contains("All Lanes", result.LaneName);
        }

        [Fact]
        public async Task GetTodayLaneAssignmentAsync_HasAssignment_ReturnsRealLane()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane A" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.ToVnTime().Date;
            _dbContext.StaffLaneAssignments.Add(new StaffLaneAssignment { StaffId = 1, LaneId = lane.LaneId, AssignedDate = targetDate, WorkShiftId = 1 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetTodayLaneAssignmentAsync(1);

            Assert.Equal(lane.LaneId, result.LaneId);
            Assert.Equal("Lane A", result.LaneName);
        }

        [Fact]
        public async Task CheckInBookingAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CheckInBookingAsync(1, 999));
        }

        [Fact]
        public async Task CheckInBookingAsync_NotPending_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51E11111", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CheckInBookingAsync(1, booking.BookingId));
        }

        [Fact]
        public async Task CheckInBookingAsync_UsesStaffLaneAssignment()
        {
            var lane = new Lane { BranchId = 1, Name = "Assigned Lane" };
            _dbContext.Lanes.Add(lane);
            var booking = new Booking { LicensePlate = "51E22222", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var today = DateTime.UtcNow.ToVnTime().Date;
            _dbContext.StaffLaneAssignments.Add(new StaffLaneAssignment { StaffId = 5, LaneId = lane.LaneId, AssignedDate = today, WorkShiftId = 1 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CheckInBookingAsync(5, booking.BookingId);

            Assert.True(result);
            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal(lane.LaneId, updated.ProcessingLaneId);
            Assert.Equal("CheckedIn", updated.Status);
            Assert.Equal(5, updated.ProcessingStaffId);
        }

        [Fact]
        public async Task CheckInBookingAsync_NoStaffAssignment_UsesBookingsExistingLane()
        {
            var lane = new Lane { BranchId = 1, Name = "Existing Lane" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();
            var booking = new Booking { LicensePlate = "51E33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0, ProcessingLaneId = lane.LaneId };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CheckInBookingAsync(99, booking.BookingId); // no assignment for staff 99

            Assert.True(result);
            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal(lane.LaneId, updated.ProcessingLaneId);
        }

        [Fact]
        public async Task CheckInBookingAsync_NoAssignmentNoBookingLane_FallsBackToFirstActiveBranchLane()
        {
            var lane = new Lane { BranchId = 1, Name = "Branch Lane", IsActive = true };
            _dbContext.Lanes.Add(lane);
            var booking = new Booking { LicensePlate = "51E44444", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.CheckInBookingAsync(99, booking.BookingId);

            Assert.True(result);
            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal(lane.LaneId, updated.ProcessingLaneId);
        }

        [Fact]
        public async Task GetAssignedBookingsAsync_NoMatchingBookings_ReturnsEmptyList()
        {
            var result = await _sut.GetAssignedBookingsAsync(1);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAssignedBookingsAsync_ReturnsPaymentStatusCompletedWhenPaid()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                LicensePlate = "51F11111",
                Status = "CheckedIn",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.ToVnTime().Date,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.Transactions.Add(new Transaction { ReferenceBookingId = booking.BookingId, TransactionType = "BookingPayment", Status = "Completed", Amount = 100000, Description = "paid", CreatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAssignedBookingsAsync(1);

            Assert.Single(result);
            Assert.Equal("Completed", result[0].PaymentStatus);
        }

        [Fact]
        public async Task GetAssignedBookingsAsync_UnpaidBooking_ReturnsUnpaidStatus()
        {
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                LicensePlate = "51F22222",
                Status = "CheckedIn",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow.ToVnTime().Date,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAssignedBookingsAsync(1);

            Assert.Single(result);
            Assert.Equal("Unpaid", result[0].PaymentStatus);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_InvalidStatus_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusAsync(1, 1, "Cancelled"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateBookingStatusAsync(1, 999, "Processing"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_ProcessingFromWrongStatus_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51F33333", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusAsync(1, booking.BookingId, "Processing"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_ProcessingFromCheckedIn_SetsProcessingStartTime()
        {
            var booking = new Booking { LicensePlate = "51F44444", Status = "CheckedIn", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.UpdateBookingStatusAsync(5, booking.BookingId, "Processing");

            Assert.True(result);
            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("Processing", updated.Status);
            Assert.NotNull(updated.ProcessingStartTime);
            Assert.Equal(5, updated.ProcessingStaffId);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_CompletedFromWrongStatus_ThrowsBadRequestException()
        {
            var booking = new Booking { LicensePlate = "51F55555", Status = "Pending", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateBookingStatusAsync(1, booking.BookingId, "Completed"));
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_CompletingFromProcessing_CalculatesDurationAndAwardsPoints()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            var user = new User { PhoneNumber = "0999600700", Email = "opstaff1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "Test", TierId = tier.TierId });
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                UserId = user.UserId,
                LicensePlate = "51F66666",
                Status = "Processing",
                BranchId = 1,
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                ProcessingStartTime = DateTime.UtcNow.AddMinutes(-20)
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _walletMock.Setup(w => w.AwardCompletionPointsAsync(user.UserId, It.IsAny<int>(), booking.BookingId)).ReturnsAsync(100);

            var result = await _sut.UpdateBookingStatusAsync(5, booking.BookingId, "Completed");

            Assert.True(result);
            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("Completed", updated.Status);
            Assert.Equal(20, updated.ActualDurationMinutes);
            _materialUsageMock.Verify(m => m.ConsumeForCompletedBookingAsync(booking.BookingId, 5), Times.Once);
            _walletMock.Verify(w => w.AwardCompletionPointsAsync(user.UserId, 100, booking.BookingId), Times.Once);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_ReCompletingAlreadyCompleted_DoesNotDoubleAwardPoints()
        {
            var booking = new Booking { LicensePlate = "51F77777", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await _sut.UpdateBookingStatusAsync(5, booking.BookingId, "Completed");

            _walletMock.Verify(w => w.AwardCompletionPointsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task SwapShiftByPhoneAsync_TargetStaffNotFound_ThrowsBadRequestException()
        {
            var dto = new SwapLaneByPhoneDTO { TargetPhoneNumber = "0999999999" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.SwapShiftByPhoneAsync(1, dto));
        }

        [Fact]
        public async Task SwapShiftByPhoneAsync_MissingAssignment_ThrowsBadRequestException()
        {
            var targetStaff = new User { PhoneNumber = "0999600800", Email = "swap1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(targetStaff);
            await _dbContext.SaveChangesAsync();

            var dto = new SwapLaneByPhoneDTO { TargetPhoneNumber = "0999600800" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.SwapShiftByPhoneAsync(1, dto));
        }

        [Fact]
        public async Task SwapShiftByPhoneAsync_Valid_SwapsLaneIds()
        {
            var lane1 = new Lane { BranchId = 1, Name = "Lane 1" };
            var lane2 = new Lane { BranchId = 1, Name = "Lane 2" };
            _dbContext.Lanes.AddRange(lane1, lane2);
            var targetStaff = new User { PhoneNumber = "0999600801", Email = "swap2@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(targetStaff);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.ToVnTime().Date;
            var currentAssignment = new StaffLaneAssignment { StaffId = 1, LaneId = lane1.LaneId, AssignedDate = targetDate, WorkShiftId = 1 };
            var targetAssignment = new StaffLaneAssignment { StaffId = targetStaff.UserId, LaneId = lane2.LaneId, AssignedDate = targetDate, WorkShiftId = 1 };
            _dbContext.StaffLaneAssignments.AddRange(currentAssignment, targetAssignment);
            await _dbContext.SaveChangesAsync();

            var dto = new SwapLaneByPhoneDTO { TargetPhoneNumber = "0999600801" };
            var result = await _sut.SwapShiftByPhoneAsync(1, dto);

            Assert.True(result);
            var updatedCurrent = await _dbContext.StaffLaneAssignments.FirstAsync(a => a.AssignmentId == currentAssignment.AssignmentId);
            var updatedTarget = await _dbContext.StaffLaneAssignments.FirstAsync(a => a.AssignmentId == targetAssignment.AssignmentId);
            Assert.Equal(lane1.LaneId, updatedCurrent.LaneId);
            Assert.Equal(lane2.LaneId, updatedTarget.LaneId);
        }
    }
}