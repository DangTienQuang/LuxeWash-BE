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
    public class ScenarioEvaluationService : IScenarioEvaluationService
    {
        private readonly AutoWashDbContext _context;

        private readonly IReflectionHelper _reflection;
        private readonly IConditionEvaluator _conditionEvaluator;
        private readonly IConfidenceCalculator _confidenceCalculator;
        private readonly IScenarioExecutionLogger _executionLogger;

        public ScenarioEvaluationService(
            AutoWashDbContext context,
            IReflectionHelper reflection,
            IConditionEvaluator conditionEvaluator,
            IConfidenceCalculator confidenceCalculator,
            IScenarioExecutionLogger executionLogger)
        {
            _context = context;
            _reflection = reflection;
            _conditionEvaluator = conditionEvaluator;
            _confidenceCalculator = confidenceCalculator;
            _executionLogger = executionLogger;
        }

        public async Task<List<ScenarioEvaluationResult>> EvaluateCustomerAsync(int customerId)
        {
            var profile = await _context.CustomerFeatureProfiles
                .FirstOrDefaultAsync(x => x.CustomerId == customerId);

            if (profile == null)
                return new();

            var scenarios = await _context.KnowledgeScenarios
                .Where(x => x.Enabled)
                .Include(x => x.Conditions)
                    .ThenInclude(c => c.Feature)
                .Include(x => x.Actions)
                .Include(x => x.Exclusions)
                    .ThenInclude(e => e.Feature)
                .Include(x => x.Explanation)
                .Include(x => x.MessageTemplates)
                .ToListAsync();

            var results = new List<ScenarioEvaluationResult>();

            foreach (var scenario in scenarios)
            {
                if (!CanExecute(scenario))
                    continue;

                if (!PassExclusions(profile, scenario))
                    continue;

                if (!EvaluateConditions(profile, scenario))
                    continue;

                var confidence = _confidenceCalculator.Calculate(profile, scenario);

                if (confidence < scenario.ConfidenceThreshold)
                    continue;

                results.Add(BuildResult(profile, scenario, confidence));

                await _executionLogger.LogAsync(
                    customerId,
                    scenario.ScenarioId,
                    confidence);
            }

            return results
                .OrderByDescending(x => x.Priority)
                .ToList();
        }
        private bool CanExecute(KnowledgeScenario scenario)
        {
            if (!scenario.Enabled)
                return false;

            if (!scenario.LastTriggeredAt.HasValue)
                return true;

            return DateTime.UtcNow >
                   scenario.LastTriggeredAt.Value.AddDays(
                       scenario.CooldownDays);
        }

        private ScenarioEvaluationResult BuildResult(CustomerFeatureProfile profile, KnowledgeScenario scenario, double confidence)
        {
            var result = new ScenarioEvaluationResult
            {
                ScenarioId = scenario.ScenarioId,
                ScenarioCode = scenario.ScenarioCode,
                ScenarioName = scenario.ScenarioName,
                Priority = scenario.Priority,
                Confidence = confidence,
                Recommendation = scenario.Description ?? string.Empty
            };

            foreach (var condition in scenario.Conditions)
            {
                var propertyName = condition.Feature.PropertyName;

                var value = _reflection.GetPropertyValue(
                    profile,
                    propertyName);

                result.MatchedFeatures[propertyName] = value;
            }

            return result;
        }

        private bool PassExclusions(CustomerFeatureProfile profile, KnowledgeScenario scenario)
        {
            foreach (var exclusion in scenario.Exclusions)
            {
                if (_conditionEvaluator.Evaluate(profile, exclusion))
                {
                    // exclusion matched -> this scenario should NOT run
                    return false;
                }
            }

            return true;
        }

        private bool EvaluateConditions(CustomerFeatureProfile profile, KnowledgeScenario scenario)
        {
            foreach (var condition in scenario.Conditions)
            {
                if (!_conditionEvaluator.Evaluate(profile, condition))
                {
                    // any failed condition -> scenario doesn't apply
                    return false;
                }
            }

            return true;
        }
    }
}
