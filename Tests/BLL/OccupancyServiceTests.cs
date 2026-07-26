using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class OccupancyServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly OccupancyService _sut;

        public OccupancyServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new OccupancyService(_dbContext);
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_NoRecords_ReturnsZero()
        {
            var result = await _sut.GetBranchOccupancyRateAsync(1, DateTime.UtcNow);

            Assert.Equal(0.0, result);
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_SlotOutsideWindow_ReturnsZero()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(20, 0, 0), EndTime = new TimeSpan(21, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(8); // window is 8:00-12:00, slot is at 20:00
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate);

            Assert.Equal(0.0, result);
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_MaxCapacityZero_ReturnsZero()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 0 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9);
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate);

            Assert.Equal(0.0, result);
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_SingleSlotInWindow_ReturnsCorrectRatio()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 20 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9);
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 10 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate);

            Assert.Equal(0.5, result);
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_MultipleSlotsInWindow_AggregatesCorrectly()
        {
            var slot1 = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            var slot2 = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.AddRange(slot1, slot2);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9); // window 9:00-13:00, both slots fall inside
            _dbContext.DailySlotCapacities.AddRange(
                new DailySlotCapacity { SlotId = slot1.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 5 },
                new DailySlotCapacity { SlotId = slot2.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 5 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate);

            // totalBooked = 10, totalMax = 20 => 0.5
            Assert.Equal(0.5, result);
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_SlotAtExactWindowStart_Included()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9); // exactly matches slot start
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate);

            Assert.Equal(0.5, result); // included since comparison is >=
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_SlotAtExactWindowEnd_Excluded()
        {
            var slot = new TimeSlot { BranchId = 1, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9); // window is 9:00-13:00, slot starts exactly at 13:00 (endDateTime)
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 1, Date = targetDate.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate);

            Assert.Equal(0.0, result); // excluded since comparison is strict 
        }

        [Fact]
        public async Task GetBranchOccupancyRateAsync_DifferentBranch_NotIncluded()
        {
            var slot = new TimeSlot { BranchId = 2, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), MaxCapacity = 10 };
            _dbContext.TimeSlots.Add(slot);
            await _dbContext.SaveChangesAsync();

            var targetDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9);
            _dbContext.DailySlotCapacities.Add(new DailySlotCapacity { SlotId = slot.SlotId, BranchId = 2, Date = targetDate.Date, BookedWeight = 5 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBranchOccupancyRateAsync(1, targetDate); // asking about branch 1

            Assert.Equal(0.0, result);
        }
    }
}