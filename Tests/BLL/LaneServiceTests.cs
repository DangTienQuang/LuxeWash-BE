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
    public class LaneServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly LaneService _sut;

        public LaneServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new LaneService(_dbContext);
        }

        [Fact]
        public async Task GetAllLanesAsync_NoFilter_ReturnsAll()
        {
            _dbContext.Lanes.AddRange(
                new Lane { BranchId = 1, Name = "Lane 1" },
                new Lane { BranchId = 2, Name = "Lane 2" }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllLanesAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAllLanesAsync_FiltersByBranch()
        {
            _dbContext.Lanes.AddRange(
                new Lane { BranchId = 1, Name = "Lane 1" },
                new Lane { BranchId = 2, Name = "Lane 2" }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllLanesAsync(branchId: 1);

            Assert.Single(result);
            Assert.Equal("Lane 1", result[0].Name);
        }

        [Fact]
        public async Task GetLaneByIdAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetLaneByIdAsync(999));
        }

        [Fact]
        public async Task GetLaneByIdAsync_Found_ReturnsDTO()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane A", IsActive = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetLaneByIdAsync(lane.LaneId);

            Assert.Equal("Lane A", result.Name);
        }

        [Fact]
        public async Task CreateLaneAsync_BranchNotFound_ThrowsNotFoundException()
        {
            var dto = new CreateLaneDTO { Name = "New Lane", BranchId = 999 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateLaneAsync(dto));
        }

        [Fact]
        public async Task CreateLaneAsync_Valid_CreatesWithDefaults()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateLaneDTO { Name = "New Lane", BranchId = branch.BranchId };
            var result = await _sut.CreateLaneAsync(dto);

            Assert.True(result.IsActive);
            Assert.False(result.IsBusinessLane);
        }

        [Fact]
        public async Task UpdateLaneAsync_LaneNotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateLaneDTO { Name = "X", BranchId = 1, IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateLaneAsync(999, dto));
        }

        [Fact]
        public async Task UpdateLaneAsync_ChangedToNonexistentBranch_ThrowsNotFoundException()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var lane = new Lane { BranchId = branch.BranchId, Name = "Lane A", IsActive = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateLaneDTO { Name = "Lane A Updated", BranchId = 999, IsActive = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateLaneAsync(lane.LaneId, dto));
        }

        [Fact]
        public async Task UpdateLaneAsync_SameBranch_NoValidationNeeded_UpdatesSuccessfully()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var lane = new Lane { BranchId = branch.BranchId, Name = "Old Name", IsActive = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateLaneDTO { Name = "New Name", BranchId = branch.BranchId, IsActive = false };
            var result = await _sut.UpdateLaneAsync(lane.LaneId, dto);

            Assert.Equal("New Name", result.Name);
            Assert.False(result.IsActive);
        }

        [Fact]
        public async Task UpdateLaneAsync_ChangedToValidDifferentBranch_UpdatesSuccessfully()
        {
            var branchA = new Branch { Name = "Branch A", IsActive = true };
            var branchB = new Branch { Name = "Branch B", IsActive = true };
            _dbContext.Branches.AddRange(branchA, branchB);
            var lane = new Lane { BranchId = branchA.BranchId, Name = "Lane A", IsActive = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateLaneDTO { Name = "Lane A", BranchId = branchB.BranchId, IsActive = true };
            var result = await _sut.UpdateLaneAsync(lane.LaneId, dto);

            Assert.Equal(branchB.BranchId, result.BranchId);
        }
    }
}