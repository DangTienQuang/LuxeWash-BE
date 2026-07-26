using System;
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
    public class BranchServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly BranchService _sut;

        public BranchServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new BranchService(_dbContext);
        }

        [Fact]
        public async Task GetAllBranchesAsync_ReturnsAllBranches()
        {
            _dbContext.Branches.AddRange(
                new Branch { Name = "Branch A", IsActive = true },
                new Branch { Name = "Branch B", IsActive = false }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllBranchesAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetBranchByIdAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBranchByIdAsync(999));
        }

        [Fact]
        public async Task GetBranchByIdAsync_Found_ReturnsDTO()
        {
            var branch = new Branch { Name = "Main Branch", Address = "123 Street", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchByIdAsync(branch.BranchId);

            Assert.Equal("Main Branch", result.Name);
            Assert.Equal("123 Street", result.Address);
        }

        [Fact]
        public async Task CreateBranchAsync_CreatesActiveByDefault()
        {
            var dto = new CreateBranchDTO { Name = "New Branch", Address = "456 Ave" };

            var result = await _sut.CreateBranchAsync(dto);

            Assert.True(result.IsActive);
            Assert.Equal("New Branch", result.Name);
            var saved = await _dbContext.Branches.FirstOrDefaultAsync(b => b.BranchId == result.BranchId);
            Assert.NotNull(saved);
        }

        [Fact]
        public async Task UpdateBranchAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateBranchDTO { Name = "X", Address = "Y", IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateBranchAsync(999, dto));
        }

        [Fact]
        public async Task UpdateBranchAsync_Valid_UpdatesAllFields()
        {
            var branch = new Branch { Name = "Old Name", Address = "Old Address", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateBranchDTO { Name = "New Name", Address = "New Address", IsActive = false };
            var result = await _sut.UpdateBranchAsync(branch.BranchId, dto);

            Assert.Equal("New Name", result.Name);
            Assert.Equal("New Address", result.Address);
            Assert.False(result.IsActive);
        }

        [Fact]
        public async Task GetBranchEmployeeSummaryAsync_BranchNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBranchEmployeeSummaryAsync(999));
        }

        [Fact]
        public async Task GetBranchEmployeeSummaryAsync_SeparatesManagersAndStaff()
        {
            var branch = new Branch { Name = "Branch X", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var managerUser = new User { PhoneNumber = "0999100001", Email = "mgr@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            var staffUser1 = new User { PhoneNumber = "0999100002", Email = "staff1@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            var staffUser2 = new User { PhoneNumber = "0999100003", Email = "staff2@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.AddRange(managerUser, staffUser1, staffUser2);
            await _dbContext.SaveChangesAsync();

            _dbContext.EmployeeProfiles.AddRange(
                new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager One", BranchId = branch.BranchId },
                new EmployeeProfile { EmployeeId = staffUser1.UserId, FullName = "Staff One", BranchId = branch.BranchId },
                new EmployeeProfile { EmployeeId = staffUser2.UserId, FullName = "Staff Two", BranchId = branch.BranchId }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchEmployeeSummaryAsync(branch.BranchId);

            Assert.Equal(1, result.TotalManagers);
            Assert.Equal(2, result.TotalStaff);
        }
    }
}