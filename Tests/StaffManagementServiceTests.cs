using System;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AutoWashPro.Tests
{
    public class StaffManagementServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly StaffManagementService _staffManagementService;

        public StaffManagementServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new AutoWashDbContext(options);

            _staffManagementService = new StaffManagementService(_context);
        }

        private async Task SeedBaseDataAsync()
        {
            _context.Users.Add(new User { UserId = 1, PhoneNumber = "0901111111", PasswordHash = "hash", Role = "Staff", Status = "Active" });
            _context.Users.Add(new User { UserId = 2, PhoneNumber = "0902222222", PasswordHash = "hash", Role = "Staff", Status = "Active" });
            _context.WorkShifts.Add(new WorkShift { WorkShiftId = 1, ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(12, 0, 0) });
            await _context.SaveChangesAsync();
        }

        // ---------------------------------------------------------
        // 1. CreateOvertimeRequestAsync Tests
        // ---------------------------------------------------------

        [Fact]
        public async Task CreateOvertimeRequestAsync_ValidRequest_CreatesRequest_TC25()
        {
            await SeedBaseDataAsync();
            var tomorrow = DateTime.UtcNow.AddDays(1).Date;
            
            var request = new CreateOvertimeRequestDTO
            {
                WorkDate = tomorrow,
                StartTime = new TimeSpan(17, 0, 0),
                EndTime = new TimeSpan(19, 0, 0),
                Reason = "Extra clean up"
            };

            var result = await _staffManagementService.CreateOvertimeRequestAsync(1, request);

            Assert.NotNull(result);
            Assert.Equal(1, result.StaffUserId);
            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task CreateOvertimeRequestAsync_ExceedsMaxHours_ThrowsException_TC26()
        {
            await SeedBaseDataAsync();
            var tomorrow = DateTime.UtcNow.AddDays(1).Date;
            
            var request = new CreateOvertimeRequestDTO
            {
                WorkDate = tomorrow,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(16, 0, 0), // 8 hours
                Reason = "Full day extra"
            };

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => 
                _staffManagementService.CreateOvertimeRequestAsync(1, request));
            
            Assert.Contains("exceeds the maximum legal limit", ex.Message);
        }

        [Fact]
        public async Task CreateOvertimeRequestAsync_PastDate_ThrowsException_TC27()
        {
            await SeedBaseDataAsync();
            var yesterday = DateTime.UtcNow.AddDays(-1).Date;
            
            var request = new CreateOvertimeRequestDTO
            {
                WorkDate = yesterday,
                StartTime = new TimeSpan(17, 0, 0),
                EndTime = new TimeSpan(19, 0, 0)
            };

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => 
                _staffManagementService.CreateOvertimeRequestAsync(1, request));
            
            Assert.Contains("past dates", ex.Message);
        }

        [Fact]
        public async Task CreateOvertimeRequestAsync_DuplicateRequest_ThrowsException_TC28()
        {
            await SeedBaseDataAsync();
            var tomorrow = DateTime.UtcNow.AddDays(1).Date;
            
            var request = new CreateOvertimeRequestDTO
            {
                WorkDate = tomorrow,
                StartTime = new TimeSpan(17, 0, 0),
                EndTime = new TimeSpan(19, 0, 0)
            };

            // First request should succeed
            await _staffManagementService.CreateOvertimeRequestAsync(1, request);

            // Second request on the same day with pending status should fail
            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => 
                _staffManagementService.CreateOvertimeRequestAsync(1, request));
            
            Assert.Contains("already have a pending overtime request", ex.Message);
        }

        // ---------------------------------------------------------
        // 2. CreateShiftSwapRequestAsync Tests
        // ---------------------------------------------------------

        [Fact]
        public async Task CreateShiftSwapRequestAsync_ValidRequest_CreatesRequest_TC29()
        {
            await SeedBaseDataAsync();
            var tomorrow = DateTime.UtcNow.AddDays(1).Date;
            
            _context.StaffShiftAssignments.Add(new StaffShiftAssignment { AssignmentId = 1, StaffUserId = 1, WorkShiftId = 1, WorkDate = tomorrow, Status = "Scheduled" });
            _context.StaffShiftAssignments.Add(new StaffShiftAssignment { AssignmentId = 2, StaffUserId = 2, WorkShiftId = 1, WorkDate = tomorrow.AddDays(1), Status = "Scheduled" });
            await _context.SaveChangesAsync();

            var request = new CreateShiftSwapRequestDTO
            {
                FromAssignmentId = 1,
                ToAssignmentId = 2
            };

            var result = await _staffManagementService.CreateShiftSwapRequestAsync(1, request);

            Assert.NotNull(result);
            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task CreateShiftSwapRequestAsync_TargetNotScheduled_ThrowsException_TC30()
        {
            await SeedBaseDataAsync();
            var tomorrow = DateTime.UtcNow.AddDays(1).Date;
            
            _context.StaffShiftAssignments.Add(new StaffShiftAssignment { AssignmentId = 1, StaffUserId = 1, WorkShiftId = 1, WorkDate = tomorrow, Status = "Scheduled" });
            _context.StaffShiftAssignments.Add(new StaffShiftAssignment { AssignmentId = 2, StaffUserId = 2, WorkShiftId = 1, WorkDate = tomorrow.AddDays(1), Status = "OnLeave" }); // Invalid status
            await _context.SaveChangesAsync();

            var request = new CreateShiftSwapRequestDTO
            {
                FromAssignmentId = 1,
                ToAssignmentId = 2
            };

            var ex = await Assert.ThrowsAsync<AutoWashPro.BLL.Exceptions.BadRequestException>(() => 
                _staffManagementService.CreateShiftSwapRequestAsync(1, request));
            
            Assert.Contains("Can only swap shifts in Scheduled status", ex.Message);
        }
    }
}
