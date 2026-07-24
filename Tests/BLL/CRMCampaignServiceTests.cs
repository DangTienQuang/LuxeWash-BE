using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
using DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class CRMCampaignServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IVoucherCampaignService> _voucherCampaignMock;
        private readonly Mock<IWeatherService> _weatherMock;
        private readonly Mock<IOccupancyService> _occupancyMock;
        private readonly Mock<ILogger<CRMCampaignService>> _loggerMock;
        private readonly CRMCampaignService _sut;

        public CRMCampaignServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _voucherCampaignMock = new Mock<IVoucherCampaignService>();
            _weatherMock = new Mock<IWeatherService>();
            _occupancyMock = new Mock<IOccupancyService>();
            _loggerMock = new Mock<ILogger<CRMCampaignService>>();

            _sut = new CRMCampaignService(_voucherCampaignMock.Object, _weatherMock.Object, _occupancyMock.Object, _dbContext, _loggerMock.Object);
        }

        [Fact]
        public async Task ProcessDailyCampaignsAsync_DelegatesToVoucherCampaignService()
        {
            var expected = new List<VoucherCampaignProcessResultDTO> { new VoucherCampaignProcessResultDTO { VoucherCode = "TEST" } };
            _voucherCampaignMock
                .Setup(v => v.ProcessDailyCampaignsAsync(It.IsAny<DateTime?>()))
                .ReturnsAsync(expected);

            var result = await _sut.ProcessDailyCampaignsAsync();

            Assert.Same(expected, result);
        }

        [Fact]
        public async Task TriggerWeatherCampaignAsync_NotRaining_ReturnsClearMessage()
        {
            _weatherMock.Setup(w => w.IsRainingNowAsync()).ReturnsAsync(false);

            var result = await _sut.TriggerWeatherCampaignAsync();

            Assert.Equal("Weather is clear. No campaign triggered.", result);
        }

        [Fact]
        public async Task TriggerWeatherCampaignAsync_Raining_VoucherMissing_CreatesVoucher()
        {
            _weatherMock.Setup(w => w.IsRainingNowAsync()).ReturnsAsync(true);

            await _sut.TriggerWeatherCampaignAsync();

            var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.Code == "RAINYDAY30");
            Assert.NotNull(voucher);
            Assert.Equal(VoucherCampaignType.Weather, voucher.CampaignType);
        }

        [Fact]
        public async Task TriggerWeatherCampaignAsync_Raining_VoucherExists_DoesNotDuplicate()
        {
            _dbContext.Vouchers.Add(new Voucher
            {
                Code = "RAINYDAY30",
                DiscountAmount = 30,
                VoucherType = VoucherType.Discount,
                CampaignType = VoucherCampaignType.Weather,
                ExpiryDays = 1,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 999999,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            });
            await _dbContext.SaveChangesAsync();

            _weatherMock.Setup(w => w.IsRainingNowAsync()).ReturnsAsync(true);
            await _sut.TriggerWeatherCampaignAsync();

            var count = await _dbContext.Vouchers.CountAsync(v => v.Code == "RAINYDAY30");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task TriggerWeatherCampaignAsync_AssignsToActiveUsersOnly()
        {
            var activeUser = new User { PhoneNumber = "0990000001", Email = "active@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            var inactiveUser = new User { PhoneNumber = "0990000002", Email = "inactive@test.com", PasswordHash = "x", Role = "Customer", Status = "Locked" };
            _dbContext.Users.AddRange(activeUser, inactiveUser);
            await _dbContext.SaveChangesAsync();

            _weatherMock.Setup(w => w.IsRainingNowAsync()).ReturnsAsync(true);
            var result = await _sut.TriggerWeatherCampaignAsync();

            Assert.Contains("assigned to 1 users", result);

            var voucher = await _dbContext.Vouchers.FirstAsync(v => v.Code == "RAINYDAY30");
            var assigned = await _dbContext.UserVouchers.Where(uv => uv.VoucherId == voucher.VoucherId).ToListAsync();
            Assert.Single(assigned);
            Assert.Equal(activeUser.UserId, assigned[0].UserId);
        }

        [Fact]
        public async Task TriggerWeatherCampaignAsync_UserAlreadyReceivedToday_SkipsDuplicate()
        {
            var voucher = new Voucher
            {
                Code = "RAINYDAY30",
                DiscountAmount = 30,
                VoucherType = VoucherType.Discount,
                CampaignType = VoucherCampaignType.Weather,
                ExpiryDays = 1,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 999999,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0990000003", Email = "already@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher
            {
                UserId = user.UserId,
                VoucherId = voucher.VoucherId,
                ReceivedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsUsed = false,
                TriggerKey = "WeatherCampaign"
            });
            await _dbContext.SaveChangesAsync();

            _weatherMock.Setup(w => w.IsRainingNowAsync()).ReturnsAsync(true);
            var result = await _sut.TriggerWeatherCampaignAsync();

            Assert.Contains("assigned to 0 users", result);
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_NoQualifyingBranches_ReturnsEarlyMessage()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.80); // busy

            var result = await _sut.TriggerSmartWeatherCampaignAsync();

            Assert.Equal("Smart Weather Campaign evaluated. No qualifying branches found.", result);
            Assert.Empty(await _dbContext.Vouchers.ToListAsync());
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_LowOccupancy_NoProlongedRain_Skipped()
        {
            var branch = new Branch { Name = "Branch B", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.20);
            _weatherMock.Setup(w => w.IsProlongedRainAsync(It.IsAny<Branch>())).ReturnsAsync(false);

            var result = await _sut.TriggerSmartWeatherCampaignAsync();

            Assert.Equal("Smart Weather Campaign evaluated. No qualifying branches found.", result);
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_QualifyingBranch_CreatesVoucherAndAssignsCustomer()
        {
            var branch = new Branch { Name = "Branch C", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0991000001", Email = "smartweather1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerFeatureProfiles.Add(new CustomerFeatureProfile { CustomerId = user.UserId, Customer = user, FavoriteBranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.30);
            _weatherMock.Setup(w => w.IsProlongedRainAsync(It.IsAny<Branch>())).ReturnsAsync(true);

            var result = await _sut.TriggerSmartWeatherCampaignAsync();

            Assert.Contains("Issued 1 branch-specific vouchers", result);

            var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.Code == $"RAIN_BR{branch.BranchId}");
            Assert.NotNull(voucher);

            var decisionHistory = await _dbContext.AIDecisionHistories.FirstOrDefaultAsync(d => d.CustomerId == user.UserId);
            Assert.NotNull(decisionHistory);
            Assert.Equal("Issue Weather Voucher", decisionHistory.ActionType);
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_InactiveCustomer_Excluded()
        {
            var branch = new Branch { Name = "Branch D", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var inactiveUser = new User { PhoneNumber = "0991000002", Email = "inactivefp@test.com", PasswordHash = "x", Role = "Customer", Status = "Locked" };
            _dbContext.Users.Add(inactiveUser);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerFeatureProfiles.Add(new CustomerFeatureProfile { CustomerId = inactiveUser.UserId, Customer = inactiveUser, FavoriteBranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.30);
            _weatherMock.Setup(w => w.IsProlongedRainAsync(It.IsAny<Branch>())).ReturnsAsync(true);

            var result = await _sut.TriggerSmartWeatherCampaignAsync();

            Assert.Contains("Issued 0 branch-specific vouchers", result);
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_AlreadyIssuedToday_SkipsDuplicate()
        {
            var branch = new Branch { Name = "Branch E", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0991000003", Email = "dupe1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerFeatureProfiles.Add(new CustomerFeatureProfile { CustomerId = user.UserId, Customer = user, FavoriteBranchId = branch.BranchId });

            var voucherCode = $"RAIN_BR{branch.BranchId}";
            var voucher = new Voucher
            {
                Code = voucherCode,
                DiscountAmount = 30,
                VoucherType = VoucherType.Discount,
                CampaignType = VoucherCampaignType.Weather,
                ExpiryDays = 1,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 999999,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher
            {
                UserId = user.UserId,
                VoucherId = voucher.VoucherId,
                ReceivedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsUsed = false,
                TriggerKey = "SmartWeatherCampaign"
            });
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.30);
            _weatherMock.Setup(w => w.IsProlongedRainAsync(It.IsAny<Branch>())).ReturnsAsync(true);

            var result = await _sut.TriggerSmartWeatherCampaignAsync();

            Assert.Contains("Issued 0 branch-specific vouchers", result);
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_NoScenarioOrCategoryExist_CreatesBoth()
        {
            var branch = new Branch { Name = "Branch F", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.80); // no qualifying branch, but scenario is fetched before filtering

            await _sut.TriggerSmartWeatherCampaignAsync();

            var scenario = await _dbContext.KnowledgeScenarios.FirstOrDefaultAsync(s => s.ScenarioCode == "WEATHER_CAMPAIGN");
            Assert.NotNull(scenario);

            var category = await _dbContext.KnowledgeCategories.FirstOrDefaultAsync();
            Assert.NotNull(category);
            Assert.Equal(scenario.CategoryId, category.CategoryId);
        }

        [Fact]
        public async Task TriggerSmartWeatherCampaignAsync_ScenarioAlreadyExists_ReusesIt()
        {
            var category = new KnowledgeCategory { Name = "Campaigns", Code = "CAMPAIGNS", Description = "x" };
            _dbContext.KnowledgeCategories.Add(category);
            await _dbContext.SaveChangesAsync();

            var existingScenario = new KnowledgeScenario
            {
                ScenarioCode = "WEATHER_CAMPAIGN",
                ScenarioName = "Existing",
                Enabled = true,
                CategoryId = category.CategoryId
            };
            _dbContext.KnowledgeScenarios.Add(existingScenario);
            await _dbContext.SaveChangesAsync();

            _occupancyMock.Setup(o => o.GetBranchOccupancyRateAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0.80);

            await _sut.TriggerSmartWeatherCampaignAsync();

            var scenarioCount = await _dbContext.KnowledgeScenarios.CountAsync(s => s.ScenarioCode == "WEATHER_CAMPAIGN");
            Assert.Equal(1, scenarioCount);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_TooBusy_ReturnsBusyMessage()
        {
            var request = new WeatherCampaignSimulationRequestDTO { BranchId = 1, IsProlongedRain = true, OccupancyRate = 0.75 };

            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Equal("Simulation: Branch is too busy. No vouchers issued.", result);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_NoProlongedRain_ReturnsNoRainMessage()
        {
            var request = new WeatherCampaignSimulationRequestDTO { BranchId = 1, IsProlongedRain = false, OccupancyRate = 0.20 };

            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Equal("Simulation: No prolonged rain. No vouchers issued.", result);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_BranchNotFound_ReturnsNotFoundMessage()
        {
            var request = new WeatherCampaignSimulationRequestDTO { BranchId = 999, IsProlongedRain = true, OccupancyRate = 0.20 };

            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Equal("Simulation: Branch 999 not found or inactive. No vouchers issued.", result);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_BranchInactive_ReturnsNotFoundMessage()
        {
            var branch = new Branch { Name = "Inactive Branch", IsActive = false };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var request = new WeatherCampaignSimulationRequestDTO { BranchId = branch.BranchId, IsProlongedRain = true, OccupancyRate = 0.20 };

            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Contains("not found or inactive", result);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_VoucherMissing_CreatesVoucher()
        {
            var branch = new Branch { Name = "Sim Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var request = new WeatherCampaignSimulationRequestDTO { BranchId = branch.BranchId, IsProlongedRain = true, OccupancyRate = 0.20 };

            await _sut.SimulateSmartWeatherCampaignAsync(request);

            var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.Code == $"RAIN_BR{branch.BranchId}");
            Assert.NotNull(voucher);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_VoucherExists_ReusesIt()
        {
            var branch = new Branch { Name = "Sim Branch B", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var voucherCode = $"RAIN_BR{branch.BranchId}";
            _dbContext.Vouchers.Add(new Voucher
            {
                Code = voucherCode,
                DiscountAmount = 30,
                VoucherType = VoucherType.Discount,
                CampaignType = VoucherCampaignType.Weather,
                ExpiryDays = 1,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 999999,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            });
            await _dbContext.SaveChangesAsync();

            var request = new WeatherCampaignSimulationRequestDTO { BranchId = branch.BranchId, IsProlongedRain = true, OccupancyRate = 0.20 };
            await _sut.SimulateSmartWeatherCampaignAsync(request);

            var count = await _dbContext.Vouchers.CountAsync(v => v.Code == voucherCode);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_TargetCustomerFound_AssignsVoucher()
        {
            var branch = new Branch { Name = "Sim Branch C", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0992000001", Email = "simcustomer1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerFeatureProfiles.Add(new CustomerFeatureProfile { CustomerId = user.UserId, Customer = user, FavoriteBranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var request = new WeatherCampaignSimulationRequestDTO { BranchId = branch.BranchId, IsProlongedRain = true, OccupancyRate = 0.20 };
            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Contains("Issued 1 vouchers", result);

            var decisionHistory = await _dbContext.AIDecisionHistories.FirstOrDefaultAsync(d => d.CustomerId == user.UserId);
            Assert.NotNull(decisionHistory);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_CustomerAtDifferentBranch_Excluded()
        {
            var branch = new Branch { Name = "Sim Branch D", IsActive = true };
            var otherBranch = new Branch { Name = "Other Branch", IsActive = true };
            _dbContext.Branches.AddRange(branch, otherBranch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0992000002", Email = "wrongbranch@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerFeatureProfiles.Add(new CustomerFeatureProfile { CustomerId = user.UserId, Customer = user, FavoriteBranchId = otherBranch.BranchId });
            await _dbContext.SaveChangesAsync();

            var request = new WeatherCampaignSimulationRequestDTO { BranchId = branch.BranchId, IsProlongedRain = true, OccupancyRate = 0.20 };
            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Contains("Issued 0 vouchers", result);
        }

        [Fact]
        public async Task SimulateSmartWeatherCampaignAsync_AlreadyIssuedToday_SkipsDuplicate()
        {
            var branch = new Branch { Name = "Sim Branch E", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0992000003", Email = "simdupe1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerFeatureProfiles.Add(new CustomerFeatureProfile { CustomerId = user.UserId, Customer = user, FavoriteBranchId = branch.BranchId });

            var voucherCode = $"RAIN_BR{branch.BranchId}";
            var voucher = new Voucher
            {
                Code = voucherCode,
                DiscountAmount = 30,
                VoucherType = VoucherType.Discount,
                CampaignType = VoucherCampaignType.Weather,
                ExpiryDays = 1,
                IsActive = true,
                MaxUsagePerUser = 1,
                MaxUsages = 999999,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.Vouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _dbContext.UserVouchers.Add(new UserVoucher
            {
                UserId = user.UserId,
                VoucherId = voucher.VoucherId,
                ReceivedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(1),
                IsUsed = false,
                TriggerKey = "SmartWeatherCampaign"
            });
            await _dbContext.SaveChangesAsync();

            var request = new WeatherCampaignSimulationRequestDTO { BranchId = branch.BranchId, IsProlongedRain = true, OccupancyRate = 0.20 };
            var result = await _sut.SimulateSmartWeatherCampaignAsync(request);

            Assert.Contains("Issued 0 vouchers", result);
        }
    }
}