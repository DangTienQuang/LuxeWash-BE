using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AutoWashPro.Tests
{
    public class OperationStaffServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly Mock<IWalletService> _mockWalletService;
        private readonly Mock<IBookingMaterialUsageService> _mockBookingMaterialUsageService;
        private readonly OperationStaffService _operationStaffService;

        public OperationStaffServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AutoWashDbContext(options);

            _mockWalletService = new Mock<IWalletService>();
            _mockBookingMaterialUsageService = new Mock<IBookingMaterialUsageService>();

            _operationStaffService = new OperationStaffService(_context, _mockWalletService.Object, _mockBookingMaterialUsageService.Object);
        }

        private async Task SeedBaseDataAsync()
        {
            _context.Users.Add(new User { UserId = 1, PhoneNumber = "0901234567", PasswordHash = "hash", Role = "OperationStaff", Status = "Active" });
            _context.Lanes.Add(new Lane { LaneId = 1, Name = "Lane 1", BranchId = 1 });
            _context.Tiers.Add(new Tier { TierId = 1, TierName = "Diamond", MinAccumulatedPoints = 1000, BookingWindowDays = 30 });
            _context.Tiers.Add(new Tier { TierId = 2, TierName = "Silver", MinAccumulatedPoints = 100, BookingWindowDays = 7 });
            await _context.SaveChangesAsync();
        }

        // ---------------------------------------------------------
        // 1. GetTodayLaneAssignmentAsync Tests
        // ---------------------------------------------------------

        [Fact]
        public async Task GetTodayLaneAssignmentAsync_AssignedLane_ReturnsLane_TC01()
        {
            await SeedBaseDataAsync();
            var today = DateTime.UtcNow.Date;
            _context.StaffLaneAssignments.Add(new StaffLaneAssignment { AssignmentId = 1, StaffId = 1, LaneId = 1, AssignedDate = today });
            await _context.SaveChangesAsync();

            var result = await _operationStaffService.GetTodayLaneAssignmentAsync(1, today);

            Assert.NotNull(result);
            Assert.Equal(1, result.LaneId);
            Assert.Equal("Lane 1", result.LaneName);
        }

        [Fact]
        public async Task GetTodayLaneAssignmentAsync_NoAssignment_ReturnsFloater_TC02()
        {
            await SeedBaseDataAsync();
            var today = DateTime.UtcNow.Date;

            // No assignment added to DB
            var result = await _operationStaffService.GetTodayLaneAssignmentAsync(1, today);

            Assert.NotNull(result);
            Assert.Equal(0, result.LaneId);
            Assert.Contains("All Lanes", result.LaneName);
        }

        // ---------------------------------------------------------
        // 2. GetAssignedBookingsAsync (Priority Queue) Tests
        // ---------------------------------------------------------

        [Fact]
        public async Task GetAssignedBookingsAsync_SortsByTierThenFIFO_TC05()
        {
            await SeedBaseDataAsync();
            var today = DateTime.UtcNow.Date;

            _context.Users.Add(new User { UserId = 2, PhoneNumber = "0901234568", PasswordHash = "hash", Role = "Customer", Status = "Active", CustomerProfile = new CustomerProfile { ProfileId = 1, TierId = 1, FullName = "Diamond Cust" } }); // Diamond
            _context.Users.Add(new User { UserId = 3, PhoneNumber = "0901234569", PasswordHash = "hash", Role = "Customer", Status = "Active", CustomerProfile = new CustomerProfile { ProfileId = 2, TierId = 2, FullName = "Silver Cust" } }); // Silver
            
            // Booking 1: Silver tier, checked in earlier
            _context.Bookings.Add(new Booking { BookingId = 1, UserId = 3, Status = "CheckedIn", ScheduledTime = today.AddHours(9), LicensePlate = "123-456" });
            // Booking 2: Diamond tier, checked in later
            _context.Bookings.Add(new Booking { BookingId = 2, UserId = 2, Status = "CheckedIn", ScheduledTime = today.AddHours(10), LicensePlate = "123-456" });
            await _context.SaveChangesAsync();

            var results = await _operationStaffService.GetAssignedBookingsAsync(1, today);

            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
            // Diamond tier (BookingId = 2) should be first because of higher MinAccumulatedPoints
            Assert.Equal(2, results[0].BookingId);
            Assert.Equal(1, results[1].BookingId);
        }

        [Fact]
        public async Task GetAssignedBookingsAsync_EmptyQueue_ReturnsEmptyList_TC07()
        {
            await SeedBaseDataAsync();
            var today = DateTime.UtcNow.Date;

            var results = await _operationStaffService.GetAssignedBookingsAsync(1, today);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        // ---------------------------------------------------------
        // 3. UpdateBookingStatusAsync (Wash State Machine) Tests
        // ---------------------------------------------------------

        [Fact]
        public async Task UpdateBookingStatusAsync_CheckedInToProcessing_UpdatesState_TC08()
        {
            await SeedBaseDataAsync();
            _context.Bookings.Add(new Booking { BookingId = 1, UserId = 1, Status = "CheckedIn", LicensePlate = "123-456" });
            await _context.SaveChangesAsync();

            var result = await _operationStaffService.UpdateBookingStatusAsync(1, 1, "Processing");
            var booking = await _context.Bookings.FindAsync(1);

            Assert.True(result);
            Assert.Equal("Processing", booking.Status);
            Assert.NotNull(booking.ProcessingStartTime);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_ProcessingToCompleted_UpdatesState_TC09()
        {
            await SeedBaseDataAsync();
            _context.Bookings.Add(new Booking { BookingId = 1, UserId = 1, Status = "Processing", ProcessingStartTime = DateTime.UtcNow.AddMinutes(-30), LicensePlate = "123-456" });
            await _context.SaveChangesAsync();

            var result = await _operationStaffService.UpdateBookingStatusAsync(1, 1, "Completed");
            var booking = await _context.Bookings.FindAsync(1);

            Assert.True(result);
            Assert.Equal("Completed", booking.Status);
            Assert.NotNull(booking.CompletedTime);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_PendingToCompleted_ThrowsException_TC10()
        {
            await SeedBaseDataAsync();
            _context.Bookings.Add(new Booking { BookingId = 1, UserId = 1, Status = "Pending", LicensePlate = "123-456" });
            await _context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => 
                _operationStaffService.UpdateBookingStatusAsync(1, 1, "Completed"));
            
            Assert.Contains("Can only complete processing vehicles", ex.Message);
        }

        [Fact]
        public async Task UpdateBookingStatusAsync_CompletedToProcessing_ThrowsException_TC11()
        {
            await SeedBaseDataAsync();
            _context.Bookings.Add(new Booking { BookingId = 1, UserId = 1, Status = "Completed", LicensePlate = "123-456" });
            await _context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => 
                _operationStaffService.UpdateBookingStatusAsync(1, 1, "Processing"));
            
            Assert.Contains("checked-in vehicles", ex.Message);
        }
    }
}
