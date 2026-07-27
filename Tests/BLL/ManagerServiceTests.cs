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
using BLL.Services.Interface;
using BLL.DTOs.Business;

namespace AutoWashPro.Tests.BLL
{
    public class ManagerServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IBranchRevenueAnalyticsService> _revenueMock;
        private readonly ManagerService _sut;

        public ManagerServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _revenueMock = new Mock<IBranchRevenueAnalyticsService>();
            _sut = new ManagerService(_dbContext, _revenueMock.Object);
        }

        private async Task<(User manager, Branch branch, EmployeeProfile profile)> SeedManagerWithBranch()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var manager = new User { PhoneNumber = "0999400" + new Random().Next(100, 999), Email = $"mgr{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(manager);
            await _dbContext.SaveChangesAsync();

            var profile = new EmployeeProfile { EmployeeId = manager.UserId, FullName = "Manager Test", BranchId = branch.BranchId };
            _dbContext.EmployeeProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            return (manager, branch, profile);
        }

        [Fact]
        public async Task GetStaffInBranchAsync_NoProfile_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetStaffInBranchAsync(999));
        }

        [Fact]
        public async Task GetStaffInBranchAsync_ProfileNoBranch_ThrowsBadRequestException()
        {
            var manager = new User { PhoneNumber = "0999400001", Email = "nobranch@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(manager);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = manager.UserId, FullName = "No Branch Mgr", BranchId = null });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetStaffInBranchAsync(manager.UserId));
        }

        [Fact]
        public async Task GetStaffInBranchAsync_ReturnsOnlyStaffInSameBranch()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400002", Email = "staff8@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff A", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStaffInBranchAsync(manager.UserId);

            Assert.Single(result);
            Assert.Equal("Staff A", result[0].FullName);
        }

        [Fact]
        public async Task AssignStaffToLaneAsync_StaffNotInBranch_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var dto = new AssignStaffToLaneDTO { StaffId = 999, LaneId = 1, AssignedDate = DateTime.UtcNow, WorkShiftId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AssignStaffToLaneAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AssignStaffToLaneAsync_LaneNotInBranch_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400003", Email = "staff9@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff B", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var dto = new AssignStaffToLaneDTO { StaffId = staffUser.UserId, LaneId = 999, AssignedDate = DateTime.UtcNow, WorkShiftId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AssignStaffToLaneAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AssignStaffToLaneAsync_NoScheduledShift_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400004", Email = "staff10@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff C", BranchId = branch.BranchId });
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var dto = new AssignStaffToLaneDTO { StaffId = staffUser.UserId, LaneId = lane.LaneId, AssignedDate = DateTime.UtcNow, WorkShiftId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AssignStaffToLaneAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AssignStaffToLaneAsync_AlreadyAssignedSameShift_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400005", Email = "staff11@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff D", BranchId = branch.BranchId });
            var lane1 = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            var lane2 = new Lane { BranchId = branch.BranchId, Name = "Lane 2" };
            _dbContext.Lanes.AddRange(lane1, lane2);
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var assignDate = DateTime.UtcNow.Date;
            _dbContext.StaffShiftAssignments.Add(new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = assignDate });
            _dbContext.StaffLaneAssignments.Add(new StaffLaneAssignment { StaffId = staffUser.UserId, LaneId = lane1.LaneId, AssignedDate = assignDate, WorkShiftId = shift.WorkShiftId });
            await _dbContext.SaveChangesAsync();

            var dto = new AssignStaffToLaneDTO { StaffId = staffUser.UserId, LaneId = lane2.LaneId, AssignedDate = assignDate, WorkShiftId = shift.WorkShiftId };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.AssignStaffToLaneAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task AssignStaffToLaneAsync_Valid_CreatesAssignment()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400006", Email = "staff12@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff E", BranchId = branch.BranchId });
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var assignDate = DateTime.UtcNow.Date;
            _dbContext.StaffShiftAssignments.Add(new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = assignDate });
            await _dbContext.SaveChangesAsync();

            var dto = new AssignStaffToLaneDTO { StaffId = staffUser.UserId, LaneId = lane.LaneId, AssignedDate = assignDate, WorkShiftId = shift.WorkShiftId };
            var result = await _sut.AssignStaffToLaneAsync(manager.UserId, dto);

            Assert.True(result);
        }

        [Fact]
        public async Task UnassignStaffFromLaneAsync_LaneNotInBranch_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UnassignStaffFromLaneAsync(manager.UserId, 999, 1));
        }

        [Fact]
        public async Task UnassignStaffFromLaneAsync_NoAssignmentFound_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UnassignStaffFromLaneAsync(manager.UserId, lane.LaneId, 999));
        }

        [Fact]
        public async Task UnassignStaffFromLaneAsync_Valid_RemovesAssignment()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date;
            var assignment = new StaffLaneAssignment { StaffId = 5, LaneId = lane.LaneId, AssignedDate = targetDate, WorkShiftId = shift.WorkShiftId };
            _dbContext.StaffLaneAssignments.Add(assignment);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.UnassignStaffFromLaneAsync(manager.UserId, lane.LaneId, 5, targetDate);

            Assert.True(result);
            var stillExists = await _dbContext.StaffLaneAssignments.AnyAsync(a => a.AssignmentId == assignment.AssignmentId);
            Assert.False(stillExists);
        }

        [Fact]
        public async Task DeactivateStaffAsync_StaffNotFound_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeactivateStaffAsync(manager.UserId, 999));
        }

        [Fact]
        public async Task DeactivateStaffAsync_AlreadyInactive_ReturnsTrueNoOp()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400007", Email = "staff13@test.com", PasswordHash = "x", Role = "Staff", Status = "Inactive" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff F", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeactivateStaffAsync(manager.UserId, staffUser.UserId);

            Assert.True(result);
        }

        [Fact]
        public async Task DeactivateStaffAsync_Active_SetsInactive()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var staffUser = new User { PhoneNumber = "0999400008", Email = "staff14@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff G", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeactivateStaffAsync(manager.UserId, staffUser.UserId);

            Assert.True(result);
            var updated = await _dbContext.Users.FirstAsync(u => u.UserId == staffUser.UserId);
            Assert.Equal("Inactive", updated.Status);
        }

        [Fact]
        public async Task CreateLaneAsync_OverridesBranchIdToManagerBranch()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var dto = new CreateLaneDTO { Name = "New Lane", BranchId = 999, IsBusinessLane = false }; // wrong branch, should be overridden

            var result = await _sut.CreateLaneAsync(manager.UserId, dto);

            Assert.Equal(branch.BranchId, result.BranchId);
        }

        [Fact]
        public async Task CreateTimeSlotAsync_StartAfterEnd_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var dto = new CreateTimeSlotDTO { StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(9, 0, 0), MaxCapacity = 10, BranchId = branch.BranchId };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateTimeSlotAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task CreateTimeSlotAsync_OverlapsExisting_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            _dbContext.TimeSlots.Add(new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), MaxCapacity = 10 });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateTimeSlotDTO { StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), MaxCapacity = 10, BranchId = branch.BranchId };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateTimeSlotAsync(manager.UserId, dto));
        }

        [Fact]
        public async Task CreateTimeSlotAsync_Valid_Creates()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var dto = new CreateTimeSlotDTO { StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, BranchId = branch.BranchId };

            var result = await _sut.CreateTimeSlotAsync(manager.UserId, dto);

            Assert.Equal(branch.BranchId, result.BranchId);
        }

        [Fact]
        public async Task UpdateLaneAsync_NotInBranch_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var otherBranch = new Branch { Name = "Other", IsActive = true };
            _dbContext.Branches.Add(otherBranch);
            var lane = new Lane { BranchId = otherBranch.BranchId, Name = "Foreign Lane" };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateLaneDTO { Name = "Updated", IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateLaneAsync(manager.UserId, lane.LaneId, dto));
        }

        [Fact]
        public async Task UpdateLaneAsync_Valid_UpdatesFields()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Old Name", IsActive = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateLaneDTO { Name = "New Name", IsActive = false };
            var result = await _sut.UpdateLaneAsync(manager.UserId, lane.LaneId, dto);

            Assert.Equal("New Name", result.Name);
            Assert.False(result.IsActive);
        }

        [Fact]
        public async Task DeleteLaneAsync_NotInBranch_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteLaneAsync(manager.UserId, 999));
        }

        [Fact]
        public async Task DeleteLaneAsync_Valid_Deactivates()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1", IsActive = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteLaneAsync(manager.UserId, lane.LaneId);

            Assert.True(result);
            var updated = await _dbContext.Lanes.FirstAsync(l => l.LaneId == lane.LaneId);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_NotFound_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var dto = new UpdateTimeSlotDTO { StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateTimeSlotAsync(manager.UserId, 999, dto));
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_OverlapsExcludingSelf_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var slot1 = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            var slot2 = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.AddRange(slot1, slot2);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateTimeSlotDTO { StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 30, 0), MaxCapacity = 10 }; // overlaps slot1 itself, fine; test slot2 overlap instead
            var dtoOverlappingOther = new UpdateTimeSlotDTO { StartTime = new TimeSpan(11, 30, 0), EndTime = new TimeSpan(12, 30, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateTimeSlotAsync(manager.UserId, slot1.SlotId, dtoOverlappingOther));
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_Valid_Updates()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var slot = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateTimeSlotDTO { StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), MaxCapacity = 20 };
            var result = await _sut.UpdateTimeSlotAsync(manager.UserId, slot.SlotId, dto);

            Assert.Equal(20, result.MaxCapacity);
        }

        [Fact]
        public async Task DeleteTimeSlotAsync_NotFound_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteTimeSlotAsync(manager.UserId, 999));
        }

        [Fact]
        public async Task DeleteTimeSlotAsync_HasBookings_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var slot = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = branch.BranchId, Date = DateTime.UtcNow.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteTimeSlotAsync(manager.UserId, slot.SlotId));
        }

        [Fact]
        public async Task DeleteTimeSlotAsync_Valid_Deletes()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var slot = new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteTimeSlotAsync(manager.UserId, slot.SlotId);

            Assert.True(result);
            var stillExists = await _dbContext.TimeSlots.AnyAsync(s => s.SlotId == slot.SlotId);
            Assert.False(stillExists);
        }

        [Fact]
        public async Task ConfirmCheckInAndAssignLaneAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var dto = new AssignBookingToLaneDTO { LaneId = 1 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ConfirmCheckInAndAssignLaneAsync(manager.UserId, 999, dto));
        }

        [Fact]
        public async Task ConfirmCheckInAndAssignLaneAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var booking = new Booking { LicensePlate = "51Z99991", Status = "Completed", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new AssignBookingToLaneDTO { LaneId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConfirmCheckInAndAssignLaneAsync(manager.UserId, booking.BookingId, dto));
        }

        [Fact]
        public async Task ConfirmCheckInAndAssignLaneAsync_InvalidLane_ThrowsBadRequestException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var booking = new Booking { LicensePlate = "51Z99992", Status = "Pending", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new AssignBookingToLaneDTO { LaneId = 999 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConfirmCheckInAndAssignLaneAsync(manager.UserId, booking.BookingId, dto));
        }

        [Fact]
        public async Task ConfirmCheckInAndAssignLaneAsync_Valid_AssignsLaneAndChecksIn()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            var booking = new Booking { LicensePlate = "51Z99993", Status = "Pending", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new AssignBookingToLaneDTO { LaneId = lane.LaneId };
            var result = await _sut.ConfirmCheckInAndAssignLaneAsync(manager.UserId, booking.BookingId, dto);

            Assert.True(result);
            var updated = await _dbContext.Bookings.FirstAsync(b => b.BookingId == booking.BookingId);
            Assert.Equal("CheckedIn", updated.Status);
            Assert.Equal(lane.LaneId, updated.ProcessingLaneId);
        }

        [Fact]
        public async Task GetCheckInBookingsInBranchAsync_ReturnsOnlyRelevantStatuses()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            _dbContext.Bookings.AddRange(
                new Booking { LicensePlate = "51Z10001", Status = "Pending", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0, BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 0 } } },
                new Booking { LicensePlate = "51Z10002", Status = "Completed", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0, BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 0 } } }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetCheckInBookingsInBranchAsync(manager.UserId);

            Assert.Single(result);
            Assert.Equal("51Z10001", result[0].LicensePlate);
        }

        [Fact]
        public async Task GetTimeSlotsInBranchAsync_ReturnsOrderedByStartTime()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            _dbContext.TimeSlots.AddRange(
                new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), MaxCapacity = 10 },
                new TimeSlot { BranchId = branch.BranchId, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetTimeSlotsInBranchAsync(manager.UserId);

            Assert.Equal(2, result.Count);
            Assert.Equal(new TimeSpan(9, 0, 0), result[0].StartTime);
        }

        [Fact]
        public async Task GetLanesInBranchAsync_IncludesAssignedStaff()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) };
            _dbContext.WorkShifts.Add(shift);
            var staffUser = new User { PhoneNumber = "0999500001", Email = "staff20@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff Assigned", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date;
            _dbContext.StaffLaneAssignments.Add(new StaffLaneAssignment { StaffId = staffUser.UserId, LaneId = lane.LaneId, AssignedDate = targetDate, WorkShiftId = shift.WorkShiftId });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetLanesInBranchAsync(manager.UserId, targetDate);

            Assert.Single(result);
            Assert.Single(result[0].AssignedStaff);
            Assert.Equal("Staff Assigned", result[0].AssignedStaff[0].FullName);
        }

        [Fact]
        public async Task GetStaffAssignedToLaneAsync_LaneNotInBranch_ThrowsNotFoundException()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetStaffAssignedToLaneAsync(manager.UserId, 999));
        }

        [Fact]
        public async Task GetStaffAssignedToLaneAsync_ReturnsAssignments()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane 1" };
            _dbContext.Lanes.Add(lane);
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0) };
            _dbContext.WorkShifts.Add(shift);
            var staffUser = new User { PhoneNumber = "0999500002", Email = "staff21@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = staffUser.UserId, FullName = "Staff H", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date;
            _dbContext.StaffLaneAssignments.Add(new StaffLaneAssignment { StaffId = staffUser.UserId, LaneId = lane.LaneId, AssignedDate = targetDate, WorkShiftId = shift.WorkShiftId });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStaffAssignedToLaneAsync(manager.UserId, lane.LaneId, targetDate);

            Assert.Single(result);
            Assert.Equal("Staff H", result[0].FullName);
        }

        [Fact]
        public async Task CheckRevenueStimulusCampaignAsync_DelegatesToRevenueService()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var expected = new MonthlyRevenueCampaignResultDTO();
            _revenueMock.Setup(r => r.CheckAndTriggerMonthlyRevenueCampaignAsync(branch.BranchId, null, null)).ReturnsAsync(expected);

            var result = await _sut.CheckRevenueStimulusCampaignAsync(manager.UserId);

            Assert.Same(expected, result);
        }

        [Fact]
        public async Task GetPendingProposalsAsync_DelegatesToRevenueService()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var expected = new List<VoucherProposalDTO>();
            _revenueMock.Setup(r => r.GetPendingProposalsAsync(branch.BranchId)).ReturnsAsync(expected);

            var result = await _sut.GetPendingProposalsAsync(manager.UserId);

            Assert.Same(expected, result);
        }

        [Fact]
        public async Task ApproveProposalAsync_DelegatesToRevenueService()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var expected = new MonthlyRevenueCampaignResultDTO();
            _revenueMock.Setup(r => r.ApproveProposalAsync(branch.BranchId, 5)).ReturnsAsync(expected);

            var result = await _sut.ApproveProposalAsync(manager.UserId, 5);

            Assert.Same(expected, result);
        }

        [Fact]
        public async Task RejectProposalAsync_DelegatesToRevenueService()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            _revenueMock.Setup(r => r.RejectProposalAsync(branch.BranchId, 5, "bad idea")).ReturnsAsync(true);

            var result = await _sut.RejectProposalAsync(manager.UserId, 5, "bad idea");

            Assert.True(result);
        }

        [Fact]
        public async Task ScanAndNotifyRelocationAsync_NoPendingBookings_ReturnsEmptyList()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();

            var result = await _sut.ScanAndNotifyRelocationAsync(manager.UserId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task ScanAndNotifyRelocationAsync_NoAlternativeBranch_ReturnsEmptyList()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var booking = new Booking { LicensePlate = "51Z10003", Status = "Pending", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow.AddHours(1), OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();
            // No other active branch exists

            var result = await _sut.ScanAndNotifyRelocationAsync(manager.UserId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task ScanAndNotifyRelocationAsync_ValidCase_CreatesVoucherAndProposal()
        {
            var (manager, branch, profile) = await SeedManagerWithBranch();
            var altBranch = new Branch { Name = "Alt Branch", IsActive = true };
            _dbContext.Branches.Add(altBranch);
            var booking = new Booking { LicensePlate = "51Z10004", Status = "Pending", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow.AddHours(1), OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.ScanAndNotifyRelocationAsync(manager.UserId);

            Assert.Single(result);
            Assert.Equal(altBranch.BranchId, result[0].AlternativeBranchId);

            var voucherCode = $"SURGE_REL_{branch.BranchId}_{booking.BookingId}";
            var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode);
            Assert.NotNull(voucher);
        }
    }
}