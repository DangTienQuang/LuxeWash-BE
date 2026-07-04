using AutoWashPro.DAL.Data;
using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class FeatureGenerationService : IFeatureGenerationService
    {
        private readonly AutoWashDbContext _context;
        private readonly IVisitFeatureCalculator _visit;
        private readonly ISpendingFeatureCalculator _spending;
        private readonly IVehicleFeatureCalculator _vehicle;
        private readonly IPromotionFeatureCalculator _promotion;
        private readonly IServicePreferenceCalculator _service;
        private readonly IBranchPreferenceCalculator _branch;
        private readonly IEngagementFeatureCalculator _engagement;

        public FeatureGenerationService(
            AutoWashDbContext context,
            IVisitFeatureCalculator visit,
            ISpendingFeatureCalculator spending,
            IVehicleFeatureCalculator vehicle,
            IPromotionFeatureCalculator promotion,
            IServicePreferenceCalculator service,
            IBranchPreferenceCalculator branch,
            IEngagementFeatureCalculator engagement)
        {
            _context = context;
            _visit = visit;
            _spending = spending;
            _vehicle = vehicle;
            _promotion = promotion;
            _service = service;
            _branch = branch;
            _engagement = engagement;
        }

        public async Task GenerateFeaturesAsync(int customerId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.UserId == customerId);

            if (user == null)
                return;

            var profile = await _context.CustomerFeatureProfiles
                .FirstOrDefaultAsync(x => x.CustomerId == customerId);

            if (profile == null)
            {
                profile = new CustomerFeatureProfile
                {
                    CustomerId = customerId
                };

                _context.CustomerFeatureProfiles.Add(profile);
            }

            var snapshot = new CustomerAnalyticsSnapshot
            {
                Customer = user,

                FeatureProfile = profile,

                Bookings = await _context.Bookings
                    .Include(x => x.BookingDetails)
                    .Where(x => x.UserId == customerId)
                    .ToListAsync(),

                Vehicles = await _context.Vehicles
                    .Include(x => x.VehicleType)
                    .Where(x => x.UserId == customerId &&
                                !x.IsDeleted)
                    .ToListAsync()
            };

            await _visit.CalculateAsync(snapshot);

            await _spending.CalculateAsync(snapshot);

            await _vehicle.CalculateAsync(snapshot);

            await _promotion.CalculateAsync(snapshot);

            await _service.CalculateAsync(snapshot);

            await _branch.CalculateAsync(snapshot);

            await _engagement.CalculateAsync(snapshot);

            profile.LastFeatureCalculation = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task GenerateAllCustomersAsync()
        {
            var customers = await _context.Users
                .Select(x => x.UserId)
                .ToListAsync();

            foreach (var customerId in customers)
            {
                await GenerateFeaturesAsync(customerId);
            }
        }
    }
}
