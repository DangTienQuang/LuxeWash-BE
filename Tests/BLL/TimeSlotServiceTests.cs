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
    public class TimeSlotServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly TimeSlotService _sut;

        public TimeSlotServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new TimeSlotService(_dbContext);
        }

        [Fact]
        public async Task GetAllTimeSlotsAsync_NoFilter_ReturnsOrderedByStartTime()
        {
            _dbContext.TimeSlots.AddRange(
                new TimeSlot { BranchId = 1, StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(15, 0, 0), MaxCapacity = 10 },
                new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllTimeSlotsAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal(new TimeSpan(9, 0, 0), result[0].StartTime);
        }

        [Fact]
        public async Task GetAllTimeSlotsAsync_FiltersByBranch()
        {
            _dbContext.TimeSlots.AddRange(
                new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 },
                new TimeSlot { BranchId = 2, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllTimeSlotsAsync(branchId: 1);

            Assert.Single(result);
        }

        [Fact]
        public async Task CreateTimeSlotAsync_StartAfterEnd_ThrowsBadRequestException()
        {
            var dto = new CreateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(9, 0, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateTimeSlotAsync(dto));
        }

        [Fact]
        public async Task CreateTimeSlotAsync_Overlaps_ThrowsBadRequestException()
        {
            _dbContext.TimeSlots.Add(new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), MaxCapacity = 10 });
            await _dbContext.SaveChangesAsync();

            var dto = new CreateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(12, 0, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateTimeSlotAsync(dto));
        }

        [Fact]
        public async Task CreateTimeSlotAsync_Valid_Creates()
        {
            var dto = new CreateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10, IsVipOnly = true };

            var result = await _sut.CreateTimeSlotAsync(dto);

            Assert.True(result.IsVipOnly);
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_StartAfterEnd_ThrowsBadRequestException()
        {
            var dto = new UpdateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(9, 0, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateTimeSlotAsync(1, dto));
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateTimeSlotAsync(999, dto));
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_OverlapsExcludingSelf_ThrowsBadRequestException()
        {
            var slot1 = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            var slot2 = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.AddRange(slot1, slot2);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(11, 30, 0), EndTime = new TimeSpan(12, 30, 0), MaxCapacity = 10 };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateTimeSlotAsync(slot1.SlotId, dto));
        }

        [Fact]
        public async Task UpdateTimeSlotAsync_Valid_Updates()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var dto = new UpdateTimeSlotDTO { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(11, 0, 0), MaxCapacity = 20 };
            var result = await _sut.UpdateTimeSlotAsync(slot.SlotId, dto);

            Assert.Equal(20, result.MaxCapacity);
        }

        [Fact]
        public async Task DeleteTimeSlotAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteTimeSlotAsync(999));
        }

        [Fact]
        public async Task DeleteTimeSlotAsync_HasBookings_ThrowsBadRequestException()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = DateTime.UtcNow.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteTimeSlotAsync(slot.SlotId));
        }

        [Fact]
        public async Task DeleteTimeSlotAsync_Valid_Deletes()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.DeleteTimeSlotAsync(slot.SlotId);

            Assert.True(result);
            var stillExists = await _dbContext.TimeSlots.AnyAsync(s => s.SlotId == slot.SlotId);
            Assert.False(stillExists);
        }
    }
}