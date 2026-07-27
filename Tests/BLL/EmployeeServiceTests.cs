using System;
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
    public class EmployeeServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly EmployeeService _sut;

        public EmployeeServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new EmployeeService(_dbContext);
        }

        [Fact]
        public async Task CreateEmployeeAsync_DuplicatePhone_ThrowsBadRequestException()
        {
            _dbContext.Users.Add(new User { PhoneNumber = "0999300001", Email = "x@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateEmployeeDTO { PhoneNumber = "0999300001", Password = "pw123456", Role = "Staff", FullName = "New Guy" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateEmployeeAsync(dto));
        }

        [Fact]
        public async Task CreateEmployeeAsync_BranchNotFound_ThrowsNotFoundException()
        {
            var dto = new CreateEmployeeDTO { PhoneNumber = "0999300002", Password = "pw123456", Role = "Staff", FullName = "New Guy", BranchId = 999 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateEmployeeAsync(dto));
        }

        [Fact]
        public async Task CreateEmployeeAsync_NoBranch_CreatesSuccessfully()
        {
            var dto = new CreateEmployeeDTO { PhoneNumber = "0999300003", Password = "pw123456", Role = "Staff", FullName = "Staff One" };

            var result = await _sut.CreateEmployeeAsync(dto);

            Assert.Equal("Staff One", result.FullName);
            Assert.Null(result.BranchId);
            Assert.Equal("Active", result.Status);
        }

        [Fact]
        public async Task CreateEmployeeAsync_WithBranch_CreatesSuccessfully()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateEmployeeDTO { PhoneNumber = "0999300004", Password = "pw123456", Role = "Manager", FullName = "Manager One", BranchId = branch.BranchId };

            var result = await _sut.CreateEmployeeAsync(dto);

            Assert.Equal(branch.BranchId, result.BranchId);
            Assert.Equal("Manager", result.Role);
        }

        [Fact]
        public async Task TransferEmployeeAsync_TargetBranchNotFound_ThrowsNotFoundException()
        {
            var dto = new TransferEmployeeDTO { BranchId = 999 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.TransferEmployeeAsync(1, dto));
        }

        [Fact]
        public async Task TransferEmployeeAsync_NoProfileNoValidUser_ThrowsNotFoundException()
        {
            var branch = new Branch { Name = "Branch B", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var dto = new TransferEmployeeDTO { BranchId = branch.BranchId };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.TransferEmployeeAsync(999, dto));
        }

        [Fact]
        public async Task TransferEmployeeAsync_NoProfileButManagerUser_CreatesProfileWithManagerName()
        {
            var oldBranch = new Branch { Name = "Old Branch", IsActive = true };
            var newBranch = new Branch { Name = "New Branch", IsActive = true };
            _dbContext.Branches.AddRange(oldBranch, newBranch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0999300005", Email = "mgr@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.ManagerProfiles.Add(new ManagerProfile { UserId = user.UserId, FullName = "Manager Zed" });
            await _dbContext.SaveChangesAsync();

            var dto = new TransferEmployeeDTO { BranchId = newBranch.BranchId };
            var result = await _sut.TransferEmployeeAsync(user.UserId, dto);

            Assert.True(result);
            var profile = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == user.UserId);
            Assert.NotNull(profile);
            Assert.Equal("Manager Zed", profile.FullName);
            Assert.Equal(newBranch.BranchId, profile.BranchId);
        }

        [Fact]
        public async Task TransferEmployeeAsync_NoProfileButStaffUser_CreatesProfileWithStaffName()
        {
            var newBranch = new Branch { Name = "New Branch", IsActive = true };
            _dbContext.Branches.Add(newBranch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0999300006", Email = "staff@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.StaffProfiles.Add(new StaffProfile { UserId = user.UserId, FullName = "Staff Zed" });
            await _dbContext.SaveChangesAsync();

            var dto = new TransferEmployeeDTO { BranchId = newBranch.BranchId };
            var result = await _sut.TransferEmployeeAsync(user.UserId, dto);

            Assert.True(result);
            var profile = await _dbContext.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == user.UserId);
            Assert.Equal("Staff Zed", profile.FullName);
        }

        [Fact]
        public async Task TransferEmployeeAsync_ExistingProfile_UpdatesBranchOnly()
        {
            var oldBranch = new Branch { Name = "Old Branch", IsActive = true };
            var newBranch = new Branch { Name = "New Branch", IsActive = true };
            _dbContext.Branches.AddRange(oldBranch, newBranch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0999300007", Email = "e@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = user.UserId, FullName = "Existing Staff", BranchId = oldBranch.BranchId });
            await _dbContext.SaveChangesAsync();

            var dto = new TransferEmployeeDTO { BranchId = newBranch.BranchId };
            var result = await _sut.TransferEmployeeAsync(user.UserId, dto);

            Assert.True(result);
            var profile = await _dbContext.EmployeeProfiles.FirstAsync(e => e.EmployeeId == user.UserId);
            Assert.Equal(newBranch.BranchId, profile.BranchId);
            Assert.Equal("Existing Staff", profile.FullName); // unchanged
        }
    }
}