using AutoWashPro.DAL.Data;
using BLL.Services.AI.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class ScenarioExecutionLogger : IScenarioExecutionLogger
    {
        private readonly AutoWashDbContext _context;

        public ScenarioExecutionLogger(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(int customerId, int scenarioId, double confidence)
        {
            var scenario = await _context.KnowledgeScenarios
                .FirstOrDefaultAsync(x => x.ScenarioId == scenarioId);

            if (scenario == null)
                return;

            scenario.TriggerCount++;

            scenario.LastTriggeredAt = DateTime.UtcNow;

            scenario.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
