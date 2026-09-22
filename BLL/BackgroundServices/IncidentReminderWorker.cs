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
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            foreach (var caseRecord in overdueCases)
            {
                try
                {
                    // This is a system action after the response deadline. It must not
                    // use the customer endpoint, which correctly rejects late input.
                    await using var transaction = await context.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable,
                        stoppingToken);

                    await customerService.SystemCancelAsync(
                        caseRecord.UserId ?? 0,
                        caseRecord.BookingId,
                        caseRecord.Id);

                    context.OutboxMessages.Add(new AutoWashPro.DAL.Entities.OutboxMessage
                    {
                        Type = "INCIDENT_ACTION_REQUIRED",
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = caseRecord.BookingId, IncidentId = caseRecord.IncidentId, Note = "Cancelled due to timeout" }),
                        CreatedAt = now,
                        NextRetryAt = now
                    });
                    await context.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-cancel case {CaseId}", caseRecord.Id);
                    // A rolled-back EF transaction does not reset tracked entity states.
                    // Clear them so one failed case cannot leak pending changes into the
                    // next case processed by this worker scope.
                    context.ChangeTracker.Clear();
                }
            }
        }
    }
}
