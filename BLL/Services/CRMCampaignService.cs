using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoWashPro.BLL.Services
{
    public class CRMCampaignService : ICRMCampaignService
    {
        private readonly IVoucherCampaignService _voucherCampaignService;
        private readonly IWeatherService _weatherService;
        private readonly IOccupancyService _occupancyService;
        private readonly AutoWashDbContext _context;
        private readonly ILogger<CRMCampaignService> _logger;

        public CRMCampaignService(
            IVoucherCampaignService voucherCampaignService,
            IWeatherService weatherService,
            IOccupancyService occupancyService,
            AutoWashDbContext context,
            ILogger<CRMCampaignService> logger)
        {
            _voucherCampaignService = voucherCampaignService;
            _weatherService = weatherService;
            _occupancyService = occupancyService;
            _context = context;
            _logger = logger;
        }

        public async Task<List<VoucherCampaignProcessResultDTO>> ProcessDailyCampaignsAsync()
        {
            return await _voucherCampaignService.ProcessDailyCampaignsAsync();
        }

        public async Task<string> TriggerWeatherCampaignAsync()
        {
            var isRaining = await _weatherService.IsRainingNowAsync();
            if (!isRaining)
            {
                return "Weather is clear. No campaign triggered.";
            }

            var voucherCode = "RAINYDAY30";
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode);

            if (voucher == null)
            {
                voucher = new Voucher
                {
                    Code = voucherCode,
                    DiscountAmount = 30,
                    VoucherType = VoucherType.Discount,
                    CampaignType = VoucherCampaignType.Weather,
                    ExpiryDays = 1,
                    IsActive = true,
                    MaxUsagePerUser = 1,
                    MaxUsages = 999999, // Large number to prevent it from running out
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddYears(1) // General template expiry, UserVoucher will enforce 1 day
                };

                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
            }

            var activeUsers = await _context.Users.Where(u => u.Status == "Active").ToListAsync();
            var today = DateTime.UtcNow.Date;
            var assignedCount = 0;

            var usersReceivedToday = await _context.UserVouchers
                .Where(uv => uv.VoucherId == voucher.VoucherId &&
                             uv.ReceivedDate.Date == today &&
                             uv.TriggerKey == "WeatherCampaign")
                .Select(uv => uv.UserId)
                .ToListAsync();

            var receivedUserIds = new HashSet<int>(usersReceivedToday);

            foreach (var user in activeUsers)
            {
                if (receivedUserIds.Add(user.UserId))
                {
                    var userVoucher = new UserVoucher
                    {
                        UserId = user.UserId,
                        VoucherId = voucher.VoucherId,
                        ReceivedDate = DateTime.UtcNow,
                        ExpiryDate = DateTime.UtcNow.AddDays(1),
                        IsUsed = false,
                        TriggerKey = "WeatherCampaign"
                    };

                    _context.UserVouchers.Add(userVoucher);
                    assignedCount++;
                }
            }

            if (assignedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return $"Rain detected. Rainy day voucher assigned to {assignedCount} users.";
        }

        public async Task<string> TriggerSmartWeatherCampaignAsync()
        {
            var targetDate = DateTime.UtcNow;
            var today = targetDate.Date;
            var branches = await _context.Branches.Where(b => b.IsActive).ToListAsync();
            int totalVouchersIssued = 0;

            // Ensure the Scenario exists safely against race conditions
            var scenario = await GetOrCreateWeatherCampaignScenarioAsync();

            // 1. Identify qualifying branches first (filtering out > 50% occupancy and non-prolonged rain)
            var qualifyingBranches = new List<Branch>();
            foreach (var branch in branches)
            {
                var occupancyRate = await _occupancyService.GetBranchOccupancyRateAsync(branch.BranchId, targetDate);
                if (occupancyRate > 0.50)
                {
                    _logger.LogInformation("Branch {BranchId} has occupancy rate {OccupancyRate:P2}. Skipping smart weather campaign.", branch.BranchId, occupancyRate);
                    continue;
                }

                var isProlongedRain = await _weatherService.IsProlongedRainAsync(branch);
                if (!isProlongedRain)
                {
                    _logger.LogInformation("Branch {BranchId} does not have prolonged rain. Skipping smart weather campaign.", branch.BranchId);
                    continue;
                }

                qualifyingBranches.Add(branch);
            }

            if (!qualifyingBranches.Any())
            {
                return "Smart Weather Campaign evaluated. No qualifying branches found.";
            }

            // 2. Pre-fetch all needed vouchers for qualifying branches in ONE database query
            var qualifyingBranchIds = qualifyingBranches.Select(b => b.BranchId).ToList();
            var branchVoucherCodes = qualifyingBranchIds.Select(id => $"RAIN_BR{id}").ToList();

            var existingVouchers = await _context.Vouchers
                .Where(v => branchVoucherCodes.Contains(v.Code))
                .ToDictionaryAsync(v => v.Code);

            // Create any missing vouchers in batch
            bool newVouchersCreated = false;
            foreach (var branchId in qualifyingBranchIds)
            {
                string voucherCode = $"RAIN_BR{branchId}";
                if (!existingVouchers.ContainsKey(voucherCode))
                {
                    var newVoucher = new Voucher
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
                    _context.Vouchers.Add(newVoucher);
                    existingVouchers[voucherCode] = newVoucher;
                    newVouchersCreated = true;
                }
            }

            if (newVouchersCreated)
            {
                await _context.SaveChangesAsync();
            }

            // 3. Pre-fetch all target customers for all qualifying branches in ONE database query
            var targetCustomersByBranch = await _context.CustomerFeatureProfiles
                .Where(cfp => cfp.FavoriteBranchId.HasValue &&
                              qualifyingBranchIds.Contains(cfp.FavoriteBranchId.Value) &&
                              cfp.Customer.Status == "Active")
                .Select(cfp => new { BranchId = cfp.FavoriteBranchId ?? 0, cfp.CustomerId })
                .ToListAsync();

            var branchCustomersMap = targetCustomersByBranch
                .GroupBy(x => x.BranchId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.CustomerId).Distinct().ToList());

            // 4. Pre-fetch existing UserVouchers issued today for any of these vouchers in ONE database query
            var voucherIds = existingVouchers.Values.Select(v => v.VoucherId).ToList();
            var issuedTodaySet = new HashSet<(int VoucherId, int UserId)>();

            if (voucherIds.Any())
            {
                var existingUserVouchers = await _context.UserVouchers
                    .Where(uv => voucherIds.Contains(uv.VoucherId) &&
                                 uv.ReceivedDate.Date == today &&
                                 uv.TriggerKey == "SmartWeatherCampaign")
                    .Select(uv => new { uv.VoucherId, uv.UserId })
                    .ToListAsync();

                foreach (var uv in existingUserVouchers)
                {
                    issuedTodaySet.Add((uv.VoucherId, uv.UserId));
                }
            }

            // 5. In-memory assignment for all branches without any database queries inside the loop
            bool hasNewAssignments = false;
            foreach (var branch in qualifyingBranches)
            {
                string voucherCode = $"RAIN_BR{branch.BranchId}";
                var voucher = existingVouchers[voucherCode];

                if (!branchCustomersMap.TryGetValue(branch.BranchId, out var customerIds))
                {
                    continue;
                }

                foreach (var customerId in customerIds)
                {
                    if (issuedTodaySet.Add((voucher.VoucherId, customerId)))
                    {
                        var userVoucher = new UserVoucher
                        {
                            UserId = customerId,
                            VoucherId = voucher.VoucherId,
                            ReceivedDate = DateTime.UtcNow,
                            ExpiryDate = DateTime.UtcNow.AddDays(1),
                            IsUsed = false,
                            TriggerKey = "SmartWeatherCampaign"
                        };
                        _context.UserVouchers.Add(userVoucher);

                        var decisionHistory = new AIDecisionHistory
                        {
                            CustomerId = customerId,
                            ScenarioId = scenario.ScenarioId,
                            VoucherId = voucher.VoucherId,
                            ActionType = "Issue Weather Voucher",
                            DecisionReason = "Prolonged rain + Occupancy below 50%",
                            Confidence = 0.95,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.AIDecisionHistories.Add(decisionHistory);

                        totalVouchersIssued++;
                        hasNewAssignments = true;
                    }
                }
            }

            if (hasNewAssignments)
            {
                await _context.SaveChangesAsync();
            }

            return $"Smart Weather Campaign executed. Issued {totalVouchersIssued} branch-specific vouchers.";
        }

        public async Task<string> SimulateSmartWeatherCampaignAsync(WeatherCampaignSimulationRequestDTO request)
        {
            if (request.OccupancyRate > 0.50)
            {
                return "Simulation: Branch is too busy. No vouchers issued.";
            }

            if (!request.IsProlongedRain)
            {
                return "Simulation: No prolonged rain. No vouchers issued.";
            }

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == request.BranchId && b.IsActive);
            if (branch == null)
            {
                return $"Simulation: Branch {request.BranchId} not found or inactive. No vouchers issued.";
            }

            var today = DateTime.UtcNow.Date;
            int totalVouchersIssued = 0;

            var scenario = await GetOrCreateWeatherCampaignScenarioAsync();

            string voucherCode = $"RAIN_BR{request.BranchId}";
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode);

            if (voucher == null)
            {
                voucher = new Voucher
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
                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
            }

            var targetCustomerIds = await _context.CustomerFeatureProfiles
                .Where(cfp => cfp.FavoriteBranchId == request.BranchId &&
                              cfp.Customer.Status == "Active")
                .Select(cfp => cfp.CustomerId)
                .Distinct()
                .ToListAsync();

            var issuedTodayUserIds = await _context.UserVouchers
                .Where(uv => uv.VoucherId == voucher.VoucherId &&
                             uv.ReceivedDate.Date == today &&
                             uv.TriggerKey == "SmartWeatherCampaign")
                .Select(uv => uv.UserId)
                .ToListAsync();

            var issuedTodaySet = new HashSet<int>(issuedTodayUserIds);
            bool hasNewAssignments = false;

            foreach (var customerId in targetCustomerIds)
            {
                if (issuedTodaySet.Add(customerId))
                {
                    var userVoucher = new UserVoucher
                    {
                        UserId = customerId,
                        VoucherId = voucher.VoucherId,
                        ReceivedDate = DateTime.UtcNow,
                        ExpiryDate = DateTime.UtcNow.AddDays(1),
                        IsUsed = false,
                        TriggerKey = "SmartWeatherCampaign"
                    };
                    _context.UserVouchers.Add(userVoucher);

                    var decisionHistory = new AIDecisionHistory
                    {
                        CustomerId = customerId,
                        ScenarioId = scenario.ScenarioId,
                        VoucherId = voucher.VoucherId,
                        ActionType = "Issue Weather Voucher",
                        DecisionReason = "Prolonged rain + Occupancy below 50%",
                        Confidence = 0.95,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AIDecisionHistories.Add(decisionHistory);

                    totalVouchersIssued++;
                    hasNewAssignments = true;
                }
            }

            if (hasNewAssignments)
            {
                await _context.SaveChangesAsync();
            }

            return $"Simulation: Smart Weather Campaign executed for Branch {request.BranchId}. Issued {totalVouchersIssued} vouchers.";
        }

        private async Task<KnowledgeScenario> GetOrCreateWeatherCampaignScenarioAsync()
        {
            var scenario = await _context.KnowledgeScenarios.FirstOrDefaultAsync(s => s.ScenarioCode == "WEATHER_CAMPAIGN");
            if (scenario != null)
            {
                return scenario;
            }

            try
            {
                var category = await _context.KnowledgeCategories.FirstOrDefaultAsync();
                if (category == null)
                {
                    category = new KnowledgeCategory { Name = "Campaigns", Code = "CAMPAIGNS", Description = "Campaigns Category" };
                    _context.KnowledgeCategories.Add(category);
                    await _context.SaveChangesAsync();
                }

                scenario = new KnowledgeScenario
                {
                    ScenarioCode = "WEATHER_CAMPAIGN",
                    ScenarioName = "Smart Weather Campaign",
                    Enabled = true,
                    CategoryId = category.CategoryId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.KnowledgeScenarios.Add(scenario);
                await _context.SaveChangesAsync();
                return scenario;
            }
            catch (DbUpdateException)
            {
                // In case of concurrent creation or duplicate key error, reload existing scenario
                _context.ChangeTracker.Clear();
                scenario = await _context.KnowledgeScenarios.FirstOrDefaultAsync(s => s.ScenarioCode == "WEATHER_CAMPAIGN");
                if (scenario != null)
                {
                    return scenario;
                }
                throw;
            }
        }
    }
}
