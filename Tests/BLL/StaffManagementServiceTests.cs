using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class StaffManagementServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly StaffManagementService _sut;

        public StaffManagementServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new StaffManagementService(_dbContext);
        }

        [Fact]
        public async Task CreateStaffAsync_DuplicatePhone_ThrowsBadRequestException()
        {
            _dbContext.Users.Add(new User { PhoneNumber = "0999600001", Email = "a@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateStaffDTO { PhoneNumber = "0999600001", Password = "pw123456", FullName = "New Staff" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateStaffAsync(dto));
        }

        [Fact]
        public async Task CreateStaffAsync_DuplicateEmail_ThrowsBadRequestException()
        {
            _dbContext.Users.Add(new User { PhoneNumber = "0999600002", Email = "dupe@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateStaffDTO { PhoneNumber = "0999600003", Email = "dupe@test.com", Password = "pw123456", FullName = "New Staff" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateStaffAsync(dto));
        }

        [Fact]
        public async Task CreateStaffAsync_Valid_CreatesStaffProfile()
        {
            var dto = new CreateStaffDTO { PhoneNumber = "0999600004", Password = "pw123456", FullName = "Staff One", Position = "Washer" };

            var result = await _sut.CreateStaffAsync(dto);

            Assert.Equal("Staff One", result.FullName);
            Assert.Equal("Staff", result.Role);
        }

        [Fact]
        public async Task CreateStaffWithRoleAsync_InvalidRole_ThrowsBadRequestException()
        {
            var dto = new CreateStaffDTO { PhoneNumber = "0999600005", Password = "pw123456", FullName = "X" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateStaffWithRoleAsync(dto, "Admin"));
        }

        [Fact]
        public async Task CreateStaffWithRoleAsync_Manager_CreatesManagerProfile()
        {
            var dto = new CreateStaffDTO { PhoneNumber = "0999600006", Password = "pw123456", FullName = "Manager One", Position = "Branch Manager" };

            var result = await _sut.CreateStaffWithRoleAsync(dto, "Manager");

            Assert.Equal("Manager", result.Role);
            Assert.Equal("Manager One", result.FullName);
        }

        [Fact]
        public async Task GetStaffByRoleAsync_WrongRole_ThrowsNotFoundException()
        {
            var user = new User { PhoneNumber = "0999600007", Email = "s1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "Staff X" });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetStaffByRoleAsync(user.UserId, "Manager"));
        }

        [Fact]
        public async Task GetStaffByRoleAsync_Valid_ReturnsDTO()
        {
            var user = new User { PhoneNumber = "0999600008", Email = "s2@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "Staff Y" });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStaffByRoleAsync(user.UserId, "Staff");

            Assert.Equal("Staff Y", result.FullName);
        }

        [Fact]
        public async Task UpdateStaffAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateStaffDTO { FullName = "X" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateStaffAsync(999, dto));
        }

        [Fact]
        public async Task UpdateStaffAsync_DuplicatePhone_ThrowsBadRequestException()
        {
            var user1 = new User { PhoneNumber = "0999600009", Email = "u1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999600010", Email = "u2@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.AddRange(
                new StaffProfile { UserId = user1.UserId, FullName = "U1" },
                new StaffProfile { UserId = user2.UserId, FullName = "U2" }
            );
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateStaffDTO { PhoneNumber = "0999600009" }; // trying to take user1's phone for user2

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateStaffAsync(user2.UserId, dto));
        }

        [Fact]
        public async Task UpdateStaffAsync_Valid_UpdatesFullNameAndPosition()
        {
            var user = new User { PhoneNumber = "0999600011", Email = "u3@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "Old Name" });
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateStaffDTO { FullName = "New Name", Position = "Senior Washer" };
            var result = await _sut.UpdateStaffAsync(user.UserId, dto);

            Assert.Equal("New Name", result.FullName);
            Assert.Equal("Senior Washer", result.Position);
        }

        [Fact]
        public async Task UpdateStaffStatusAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999600012", Email = "u4@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "U4" });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateStaffStatusAsync(user.UserId, "Pending"));
        }

        [Fact]
        public async Task UpdateStaffStatusAsync_Valid_UpdatesStatus()
        {
            var user = new User { PhoneNumber = "0999600013", Email = "u5@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "U5" });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.UpdateStaffStatusAsync(user.UserId, "Blocked");

            Assert.True(result);
            var updated = await _dbContext.Users.FirstAsync(u => u.UserId == user.UserId);
            Assert.Equal("Blocked", updated.Status);
        }

        [Fact]
        public async Task SoftDeleteStaffByRoleAsync_WrongRole_ThrowsNotFoundException()
        {
            var user = new User { PhoneNumber = "0999600014", Email = "u6@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "U6" });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.SoftDeleteStaffByRoleAsync(user.UserId, "Manager"));
        }

        [Fact]
        public async Task SoftDeleteStaffByRoleAsync_Valid_SetsBlocked()
        {
            var user = new User { PhoneNumber = "0999600015", Email = "u7@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "U7" });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.SoftDeleteStaffByRoleAsync(user.UserId, "Staff");

            Assert.True(result);
            var updated = await _dbContext.Users.FirstAsync(u => u.UserId == user.UserId);
            Assert.Equal("Blocked", updated.Status);
        }

        [Fact]
        public async Task GetStaffsAsync_FiltersByKeyword()
        {
            var user1 = new User { PhoneNumber = "0999600016", Email = "alpha@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999600017", Email = "beta@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.AddRange(
                new StaffProfile { UserId = user1.UserId, FullName = "Alpha Person" },
                new StaffProfile { UserId = user2.UserId, FullName = "Beta Person" }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetStaffsAsync("alpha", null, null);

            Assert.Single(result);
            Assert.Equal("Alpha Person", result[0].FullName);
        }

        [Fact]
        public async Task GetWorkShiftsAsync_ExcludesInactiveByDefault()
        {
            _dbContext.WorkShifts.AddRange(
                new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true },
                new WorkShift { ShiftName = "Old Shift", StartTime = new TimeSpan(0, 0, 0), EndTime = new TimeSpan(4, 0, 0), IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetWorkShiftsAsync(includeInactive: false);

            Assert.Single(result);
        }

        [Fact]
        public async Task CreateWorkShiftAsync_StartAfterEnd_ThrowsBadRequestException()
        {
            var dto = new CreateWorkShiftDTO { ShiftName = "Bad Shift", StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(8, 0, 0) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWorkShiftAsync(dto));
        }

        [Fact]
        public async Task CreateWorkShiftAsync_DuplicateName_ThrowsBadRequestException()
        {
            _dbContext.WorkShifts.Add(new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateWorkShiftDTO { ShiftName = "morning", StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(17, 0, 0) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateWorkShiftAsync(dto));
        }

        [Fact]
        public async Task CreateWorkShiftAsync_Valid_Creates()
        {
            var dto = new CreateWorkShiftDTO { ShiftName = "Evening", StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(22, 0, 0) };

            var result = await _sut.CreateWorkShiftAsync(dto);

            Assert.Equal("Evening", result.ShiftName);
        }

        [Fact]
        public async Task UpdateWorkShiftAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateWorkShiftDTO { ShiftName = "X", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateWorkShiftAsync(999, dto));
        }

        [Fact]
        public async Task UpdateWorkShiftAsync_Valid_Updates()
        {
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateWorkShiftDTO { ShiftName = "Morning Shift", StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0), IsActive = true };
            var result = await _sut.UpdateWorkShiftAsync(shift.WorkShiftId, dto);

            Assert.Equal("Morning Shift", result.ShiftName);
        }

        [Fact]
        public async Task DeleteWorkShiftAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteWorkShiftAsync(999));
        }

        [Fact]
        public async Task DeleteWorkShiftAsync_HasAssignments_DeactivatesInsteadOfDeleting()
        {
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            var staffUser = new User { PhoneNumber = "0999600018", Email = "s3@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffShiftAssignments.Add(new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteWorkShiftAsync(shift.WorkShiftId);

            Assert.True(result);
            var updated = await _dbContext.WorkShifts.FirstAsync(s => s.WorkShiftId == shift.WorkShiftId);
            Assert.False(updated.IsActive); // deactivated, not deleted
        }

        [Fact]
        public async Task DeleteWorkShiftAsync_NoAssignments_DeletesFully()
        {
            var shift = new WorkShift { ShiftName = "Unused Shift", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteWorkShiftAsync(shift.WorkShiftId);

            Assert.True(result);
            var stillExists = await _dbContext.WorkShifts.AnyAsync(s => s.WorkShiftId == shift.WorkShiftId);
            Assert.False(stillExists);
        }

        [Fact]
        public async Task CreateShiftAssignmentAsync_StaffNotFound_ThrowsNotFoundException()
        {
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateShiftAssignmentDTO { StaffUserId = 999, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateShiftAssignmentAsync(dto));
        }

        [Fact]
        public async Task CreateShiftAssignmentAsync_ShiftNotFoundOrInactive_ThrowsNotFoundException()
        {
            var staffUser = new User { PhoneNumber = "0999600019", Email = "s4@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "S4" });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateShiftAssignmentDTO { StaffUserId = staffUser.UserId, WorkShiftId = 999, WorkDate = DateTime.UtcNow };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateShiftAssignmentAsync(dto));
        }

        [Fact]
        public async Task CreateShiftAssignmentAsync_ConflictingAssignment_ThrowsBadRequestException()
        {
            var staffUser = new User { PhoneNumber = "0999600020", Email = "s5@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "S5" });
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var workDate = DateTime.UtcNow.Date;
            _dbContext.StaffShiftAssignments.Add(new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = workDate });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateShiftAssignmentDTO { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = workDate };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateShiftAssignmentAsync(dto));
        }

        [Fact]
        public async Task CreateShiftAssignmentAsync_Valid_CreatesAssignment()
        {
            var staffUser = new User { PhoneNumber = "0999600021", Email = "s6@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "S6" });
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateShiftAssignmentDTO { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow };
            var result = await _sut.CreateShiftAssignmentAsync(dto);

            Assert.Equal("S6", result.StaffName);
            Assert.Equal("Scheduled", result.Status);
        }

        [Fact]
        public async Task DeleteShiftAssignmentAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteShiftAssignmentAsync(999));
        }

        [Fact]
        public async Task DeleteShiftAssignmentAsync_HasPendingSwap_ThrowsBadRequestException()
        {
            var staffUser = new User { PhoneNumber = "0999600022", Email = "s7@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "S7" });
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var assignment1 = new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date };
            var assignment2 = new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date.AddDays(1) };
            _dbContext.StaffShiftAssignments.AddRange(assignment1, assignment2);
            await _dbContext.SaveChangesAsync();

            _dbContext.ShiftSwapRequests.Add(new ShiftSwapRequest { FromAssignmentId = assignment1.AssignmentId, ToAssignmentId = assignment2.AssignmentId, RequestedByUserId = staffUser.UserId, Status = "Pending" });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteShiftAssignmentAsync(assignment1.AssignmentId));
        }

        [Fact]
        public async Task DeleteShiftAssignmentAsync_Valid_Deletes()
        {
            var staffUser = new User { PhoneNumber = "0999600023", Email = "s8@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "S8" });
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var assignment = new StaffShiftAssignment { StaffUserId = staffUser.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date };
            _dbContext.StaffShiftAssignments.Add(assignment);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteShiftAssignmentAsync(assignment.AssignmentId);

            Assert.True(result);
        }

        [Fact]
        public async Task CreateOvertimeRequestAsync_StartAfterEnd_ThrowsBadRequestException()
        {
            var staffUser = new User { PhoneNumber = "0999600024", Email = "ot1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "OT1" });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateOvertimeRequestDTO { WorkDate = DateTime.UtcNow, StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(16, 0, 0) };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateOvertimeRequestAsync(staffUser.UserId, dto));
        }

        [Fact]
        public async Task CreateOvertimeRequestAsync_StaffNotFound_ThrowsNotFoundException()
        {
            var dto = new CreateOvertimeRequestDTO { WorkDate = DateTime.UtcNow, StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0) };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateOvertimeRequestAsync(999, dto));
        }

        [Fact]
        public async Task CreateOvertimeRequestAsync_Valid_CreatesPending()
        {
            var staffUser = new User { PhoneNumber = "0999600025", Email = "ot2@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "OT2" });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateOvertimeRequestDTO { WorkDate = DateTime.UtcNow, StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0), Reason = "extra shift" };
            var result = await _sut.CreateOvertimeRequestAsync(staffUser.UserId, dto);

            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task ReviewOvertimeRequestAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new ReviewRequestDTO { IsApproved = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ReviewOvertimeRequestAsync(999, 1, dto));
        }

        [Fact]
        public async Task ReviewOvertimeRequestAsync_AlreadyProcessed_ThrowsBadRequestException()
        {
            var staffUser = new User { PhoneNumber = "0999600026", Email = "ot3@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "OT3" });

            var overtime = new OvertimeRequest { StaffUserId = staffUser.UserId, WorkDate = DateTime.UtcNow, StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0), Status = "Approved" };
            _dbContext.OvertimeRequests.Add(overtime);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewRequestDTO { IsApproved = false };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ReviewOvertimeRequestAsync(overtime.OvertimeRequestId, 1, dto));
        }

        [Fact]
        public async Task ReviewOvertimeRequestAsync_Approve_UpdatesStatus()
        {
            var staffUser = new User { PhoneNumber = "0999600027", Email = "ot4@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = staffUser.UserId, FullName = "OT4" });

            var overtime = new OvertimeRequest { StaffUserId = staffUser.UserId, WorkDate = DateTime.UtcNow, StartTime = new TimeSpan(16, 0, 0), EndTime = new TimeSpan(18, 0, 0), Status = "Pending" };
            _dbContext.OvertimeRequests.Add(overtime);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewRequestDTO { IsApproved = true, ReviewNote = "ok" };
            var result = await _sut.ReviewOvertimeRequestAsync(overtime.OvertimeRequestId, 1, dto);

            Assert.Equal("Approved", result.Status);
        }

        [Fact]
        public async Task CreateShiftSwapRequestAsync_SameAssignment_ThrowsBadRequestException()
        {
            var dto = new CreateShiftSwapRequestDTO { FromAssignmentId = 1, ToAssignmentId = 1 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateShiftSwapRequestAsync(1, dto));
        }

        [Fact]
        public async Task CreateShiftSwapRequestAsync_AssignmentNotFound_ThrowsNotFoundException()
        {
            var dto = new CreateShiftSwapRequestDTO { FromAssignmentId = 1, ToAssignmentId = 2 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateShiftSwapRequestAsync(1, dto));
        }

        [Fact]
        public async Task CreateShiftSwapRequestAsync_NotOwnShift_ThrowsBadRequestException()
        {
            var staffA = new User { PhoneNumber = "0999600028", Email = "sw1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var staffB = new User { PhoneNumber = "0999600029", Email = "sw2@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(staffA, staffB);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.AddRange(
                new StaffProfile { UserId = staffA.UserId, FullName = "SwA" },
                new StaffProfile { UserId = staffB.UserId, FullName = "SwB" }
            );
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var fromAssignment = new StaffShiftAssignment { StaffUserId = staffA.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date, Status = "Scheduled" };
            var toAssignment = new StaffShiftAssignment { StaffUserId = staffB.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date.AddDays(1), Status = "Scheduled" };
            _dbContext.StaffShiftAssignments.AddRange(fromAssignment, toAssignment);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateShiftSwapRequestDTO { FromAssignmentId = fromAssignment.AssignmentId, ToAssignmentId = toAssignment.AssignmentId };

            // staffB tries to submit swap request claiming fromAssignment (which belongs to staffA)
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateShiftSwapRequestAsync(staffB.UserId, dto));
        }

        [Fact]
        public async Task CreateShiftSwapRequestAsync_Valid_CreatesPendingSwap()
        {
            var staffA = new User { PhoneNumber = "0999600030", Email = "sw3@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var staffB = new User { PhoneNumber = "0999600031", Email = "sw4@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(staffA, staffB);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.AddRange(
                new StaffProfile { UserId = staffA.UserId, FullName = "SwC" },
                new StaffProfile { UserId = staffB.UserId, FullName = "SwD" }
            );
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var fromAssignment = new StaffShiftAssignment { StaffUserId = staffA.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date, Status = "Scheduled" };
            var toAssignment = new StaffShiftAssignment { StaffUserId = staffB.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date.AddDays(1), Status = "Scheduled" };
            _dbContext.StaffShiftAssignments.AddRange(fromAssignment, toAssignment);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateShiftSwapRequestDTO { FromAssignmentId = fromAssignment.AssignmentId, ToAssignmentId = toAssignment.AssignmentId, Reason = "personal" };
            var result = await _sut.CreateShiftSwapRequestAsync(staffA.UserId, dto);

            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task ReviewShiftSwapRequestAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new ReviewRequestDTO { IsApproved = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ReviewShiftSwapRequestAsync(999, 1, dto));
        }

        [Fact]
        public async Task ReviewShiftSwapRequestAsync_Approve_SwapsStaffOnBothAssignments()
        {
            var staffA = new User { PhoneNumber = "0999600032", Email = "sw5@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var staffB = new User { PhoneNumber = "0999600033", Email = "sw6@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(staffA, staffB);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.AddRange(
                new StaffProfile { UserId = staffA.UserId, FullName = "SwE" },
                new StaffProfile { UserId = staffB.UserId, FullName = "SwF" }
            );
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var fromAssignment = new StaffShiftAssignment { StaffUserId = staffA.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date, Status = "Scheduled" };
            var toAssignment = new StaffShiftAssignment { StaffUserId = staffB.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date.AddDays(1), Status = "Scheduled" };
            _dbContext.StaffShiftAssignments.AddRange(fromAssignment, toAssignment);
            await _dbContext.SaveChangesAsync();

            var swap = new ShiftSwapRequest { FromAssignmentId = fromAssignment.AssignmentId, ToAssignmentId = toAssignment.AssignmentId, RequestedByUserId = staffA.UserId, Status = "Pending" };
            _dbContext.ShiftSwapRequests.Add(swap);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewRequestDTO { IsApproved = true, ReviewNote = "approved" };
            var result = await _sut.ReviewShiftSwapRequestAsync(swap.ShiftSwapRequestId, 1, dto);

            Assert.Equal("Approved", result.Status);

            var updatedFrom = await _dbContext.StaffShiftAssignments.FirstAsync(a => a.AssignmentId == fromAssignment.AssignmentId);
            var updatedTo = await _dbContext.StaffShiftAssignments.FirstAsync(a => a.AssignmentId == toAssignment.AssignmentId);
            Assert.Equal(staffB.UserId, updatedFrom.StaffUserId); // swapped
            Assert.Equal(staffA.UserId, updatedTo.StaffUserId); // swapped
        }

        [Fact]
        public async Task ReviewShiftSwapRequestAsync_Reject_DoesNotSwapStaff()
        {
            var staffA = new User { PhoneNumber = "0999600034", Email = "sw7@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var staffB = new User { PhoneNumber = "0999600035", Email = "sw8@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(staffA, staffB);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.AddRange(
                new StaffProfile { UserId = staffA.UserId, FullName = "SwG" },
                new StaffProfile { UserId = staffB.UserId, FullName = "SwH" }
            );
            var shift = new WorkShift { ShiftName = "Morning", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(16, 0, 0), IsActive = true };
            _dbContext.WorkShifts.Add(shift);
            await _dbContext.SaveChangesAsync();

            var fromAssignment = new StaffShiftAssignment { StaffUserId = staffA.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date, Status = "Scheduled" };
            var toAssignment = new StaffShiftAssignment { StaffUserId = staffB.UserId, WorkShiftId = shift.WorkShiftId, WorkDate = DateTime.UtcNow.Date.AddDays(1), Status = "Scheduled" };
            _dbContext.StaffShiftAssignments.AddRange(fromAssignment, toAssignment);
            await _dbContext.SaveChangesAsync();

            var swap = new ShiftSwapRequest { FromAssignmentId = fromAssignment.AssignmentId, ToAssignmentId = toAssignment.AssignmentId, RequestedByUserId = staffA.UserId, Status = "Pending" };
            _dbContext.ShiftSwapRequests.Add(swap);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewRequestDTO { IsApproved = false, ReviewNote = "denied" };
            var result = await _sut.ReviewShiftSwapRequestAsync(swap.ShiftSwapRequestId, 1, dto);

            Assert.Equal("Rejected", result.Status);

            var updatedFrom = await _dbContext.StaffShiftAssignments.FirstAsync(a => a.AssignmentId == fromAssignment.AssignmentId);
            Assert.Equal(staffA.UserId, updatedFrom.StaffUserId); // unchanged
        }
    }
}