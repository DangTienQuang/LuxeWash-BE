using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Calculators
{
    public class EngagementFeatureCalculator : IEngagementFeatureCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            profile.LoyaltyScore = CalculateLoyalty(profile);

            profile.EngagementScore = CalculateEngagement(profile);

            profile.PriceSensitivityScore = CalculatePriceSensitivity(profile);

            profile.PredictedChurnScore = CalculateChurn(profile);

            profile.PredictedUpgradeScore = CalculateUpgrade(profile);

            profile.PredictedLifetimeValue = CalculateLifetimeValue(profile);

            return Task.CompletedTask;
        }

        private double CalculateLoyalty(CustomerFeatureProfile profile)
        {
            double score = 0;

            score += Math.Min(profile.CompletedVisitCount * 2.5, 30);

            score += Math.Min((double)(profile.LifetimeSpend / 100000m), 30);

            score += Math.Min(profile.BranchLoyaltyRate / 2, 20);

            score += Math.Min(profile.CouponUsageRate / 5, 10);

            score += Math.Min(profile.VehicleCount * 5, 10);

            return Math.Round(Math.Min(score, 100), 2);
        }

        private double CalculateEngagement(CustomerFeatureProfile profile)
        {
            double score = 100;

            score -= Math.Min(profile.DaysSinceLastVisit, 60);

            score -= profile.NoShowCount * 5;

            score -= profile.CancelledVisitCount * 2;

            score += Math.Min(profile.CompletedVisitCount, 20);

            return Math.Max(0, Math.Min(score, 100));
        }

        private double CalculatePriceSensitivity(CustomerFeatureProfile profile)
        {
            double score = 0;

            score += profile.CouponUsageRate * 0.45;

            score += profile.PointUsageRate * 0.30;

            if (profile.AverageSpend > 0)
            {
                score += (double)(
                    profile.AverageDiscountReceived /
                    profile.AverageSpend * 100m) * 0.25;
            }

            return Math.Round(Math.Min(score, 100), 2);
        }

        private double CalculateChurn(CustomerFeatureProfile profile)
        {
            double score = 0;

            if (profile.ExpectedNextVisit.HasValue)
            {
                var overdue =
                    (DateTime.UtcNow -
                    profile.ExpectedNextVisit.Value).TotalDays;

                if (overdue > 0)
                    score += overdue;
            }

            score += profile.NoShowCount * 10;

            score += profile.CancelledVisitCount * 5;

            score -= profile.LoyaltyScore * 0.30;

            return Math.Round(Math.Max(0, Math.Min(score, 100)), 2);
        }

        private double CalculateUpgrade(CustomerFeatureProfile profile)
        {
            double score = 0;

            score += profile.LoyaltyScore * 0.30;

            score += profile.EngagementScore * 0.25;

            score += profile.CompletedVisitCount;

            score += (double)(profile.AverageServicesPerBooking * 5);

            score += profile.BranchLoyaltyRate * 0.10;

            return Math.Round(Math.Min(score, 100), 2);
        }

        private decimal CalculateLifetimeValue(CustomerFeatureProfile profile)
        {
            if (profile.CompletedVisitCount == 0)
                return 0;

            var estimatedRemainingVisits =
                Math.Max(12 - profile.CompletedVisitCount, 1);

            return profile.LifetimeSpend +
                   profile.AverageSpend * estimatedRemainingVisits;
        }
    }
}
