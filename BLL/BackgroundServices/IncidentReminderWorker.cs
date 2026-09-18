using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoWashPro.BLL.BackgroundServices
{
    public class IncidentReminderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IncidentReminderWorker> _logger;

        public IncidentReminderWorker(IServiceProvider serviceProvider, ILogger<IncidentReminderWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Incident Reminder Worker starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOverdueDecisionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing overdue incident decisions.");
                }

                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessOverdueDecisionsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
            var customerService = scope.ServiceProvider.GetRequiredService<AutoWashPro.BLL.Services.Interface.IIncidentCustomerService>();
            
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            
            var overdueCases = await context.IncidentAffectedBookings
                .Where(c => c.Status == "AwaitingCustomer" && c.ResponseDeadlineAtVn <= now)
                .ToListAsync(stoppingToken);

            foreach (var caseRecord in overdueCases)
            {
                try
                {
                    // Auto-cancel
                    await customerService.HandleCustomerDecisionAsync(caseRecord.UserId ?? 0, caseRecord.Id, "Cancel", null, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-cancel case {CaseId}", caseRecord.Id);
                }
            }
        }
    }
}
