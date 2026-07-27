using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class InventoryReportServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly InventoryReportService _sut;

        public InventoryReportServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new InventoryReportService(_dbContext);
        }

        [Fact]
        public async Task GetAdminProfitReportAsync_NoCompletedBookings_ReturnsZeros()
        {
            var result = await _sut.GetAdminProfitReportAsync(null, null, null);

            Assert.Equal(0, result.Revenue);
            Assert.Equal(0, result.MaterialCost);
            Assert.Equal(0, result.GrossProfit);
            Assert.Equal(0, result.GrossMargin);
            Assert.Equal(0, result.CompletedBookings);
        }

        [Fact]
        public async Task GetAdminProfitReportAsync_ExcludesNonCompletedBookings()
        {
            _dbContext.Bookings.AddRange(
                new Booking { LicensePlate = "51X11111", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 },
                new Booking { LicensePlate = "51X22222", Status = "Cancelled", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 50000, FinalAmount = 50000 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAdminProfitReportAsync(null, null, null);

            Assert.Equal(100000, result.Revenue);
            Assert.Equal(1, result.CompletedBookings);
        }

        [Fact]
        public async Task GetAdminProfitReportAsync_ComputesGrossProfitAndMargin()
        {
            var booking = new Booking { LicensePlate = "51X33333", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 200000, FinalAmount = 200000 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "liter", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            _dbContext.BookingMaterialUsages.Add(new BookingMaterialUsage
            {
                BookingId = booking.BookingId,
                BranchId = 1,
                MaterialId = material.MaterialId,
                QuantityUsed = 2,
                UnitCost = 10000,
                CostAmount = 20000,
                UsageType = "Standard"
            });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAdminProfitReportAsync(null, null, null);

            Assert.Equal(200000, result.Revenue);
            Assert.Equal(20000, result.MaterialCost);
            Assert.Equal(180000, result.GrossProfit);
            Assert.Equal(90, result.GrossMargin); // 180000/200000 * 100 = 90%
        }

        [Fact]
        public async Task GetAdminProfitReportAsync_FiltersByBranch()
        {
            _dbContext.Bookings.AddRange(
                new Booking { LicensePlate = "51X44444", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 },
                new Booking { LicensePlate = "51X55555", Status = "Completed", BranchId = 2, ScheduledTime = DateTime.UtcNow, OriginalPrice = 50000, FinalAmount = 50000 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAdminProfitReportAsync(null, null, 1);

            Assert.Equal(100000, result.Revenue);
            Assert.Equal(1, result.CompletedBookings);
        }

        [Fact]
        public async Task GetAdminProfitReportAsync_FiltersByDateRange()
        {
            _dbContext.Bookings.AddRange(
                new Booking { LicensePlate = "51X66666", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow.AddDays(-10), OriginalPrice = 100000, FinalAmount = 100000 },
                new Booking { LicensePlate = "51X77777", Status = "Completed", BranchId = 1, ScheduledTime = DateTime.UtcNow, OriginalPrice = 50000, FinalAmount = 50000 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAdminProfitReportAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null);

            Assert.Equal(50000, result.Revenue);
            Assert.Equal(1, result.CompletedBookings);
        }

        [Fact]
        public async Task GetManagerProfitReportAsync_ManagerNotAssignedToBranch_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetManagerProfitReportAsync(999, null, null));
        }

        [Fact]
        public async Task GetManagerProfitReportAsync_ScopesToManagerBranch()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var manager = new User { PhoneNumber = "0999700001", Email = "mgr9@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(manager);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = manager.UserId, FullName = "Mgr", BranchId = branch.BranchId });

            _dbContext.Bookings.AddRange(
                new Booking { LicensePlate = "51X88888", Status = "Completed", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 100000, FinalAmount = 100000 },
                new Booking { LicensePlate = "51X99999", Status = "Completed", BranchId = 999, ScheduledTime = DateTime.UtcNow, OriginalPrice = 50000, FinalAmount = 50000 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerProfitReportAsync(manager.UserId, null, null);

            Assert.Equal(100000, result.Revenue);
            Assert.Equal(1, result.CompletedBookings);
        }
    }
}