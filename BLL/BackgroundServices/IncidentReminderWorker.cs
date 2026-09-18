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
                .Include(c => c.Incident)
                .Where(c => c.Status == "AwaitingCustomer" && c.ResponseDeadlineAtVn <= now)
                .ToListAsync(stoppingToken);

            foreach (var caseRecord in overdueCases)
            {
                try
                {
                    // Auto-cancel using the same path
                    var request = new AutoWashPro.BLL.DTOs.IncidentDecisionRequestDTO
                    {
                        IncidentId = caseRecord.IncidentId,
                        CaseId = caseRecord.Id,
                        ExpectedVersion = caseRecord.Incident?.Version ?? 1,
                        Decision = "Cancel"
                    };
                    await customerService.ProcessIncidentDecisionAsync(caseRecord.UserId ?? 0, caseRecord.BookingId, request);

                    context.OutboxMessages.Add(new AutoWashPro.DAL.Entities.OutboxMessage
                    {
                        Type = "INCIDENT_ACTION_REQUIRED",
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = caseRecord.BookingId, IncidentId = caseRecord.IncidentId, Note = "Cancelled due to timeout" }),
                        CreatedAt = now,
                        NextRetryAt = now
                    });
                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-cancel case {CaseId}", caseRecord.Id);
                }
            }
        }
    }
}
