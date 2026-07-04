using BLL.Services.AI.Interfaces;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Calculators
{
    public class ConfidenceCalculator : IConfidenceCalculator
    {
        public double Calculate(CustomerFeatureProfile profile, KnowledgeScenario scenario)
        {
            double confidence = 50;

            confidence += Math.Min(profile.LoyaltyScore * 0.2, 10);

            confidence += Math.Min(profile.EngagementScore * 0.2, 10);

            confidence += Math.Min(profile.PremiumPreferenceScore * 0.1, 5);

            confidence += Math.Min(profile.PredictedUpgradeScore * 0.1, 5);

            confidence -= Math.Min(profile.PredictedChurnScore * 0.05, 5);

            confidence = Math.Max(0, Math.Min(confidence, 100));

            return confidence;
        }
    }
}
