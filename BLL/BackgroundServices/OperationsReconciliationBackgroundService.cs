using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AutoWashPro.BLL.Services.Operations;

namespace AutoWashPro.BLL.BackgroundServices
{
    public class OperationsReconciliationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OperationsReconciliationBackgroundService> _logger;

        public OperationsReconciliationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<OperationsReconciliationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Operations Reconciliation Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var monitoringService = scope.ServiceProvider.GetRequiredService<IOperationsMonitoringService>();

                    // In a real application, we would loop over all active branches.
                    // Assuming branch 1 is the primary branch for this check.
                    int primaryBranchId = 1; 

                    var alerts = await monitoringService.RunReconciliationCheckAsync(primaryBranchId, stoppingToken);

                    if (alerts.Count > 0)
                    {
                        _logger.LogWarning("Reconciliation check found {AlertCount} inconsistencies for Branch {BranchId}", alerts.Count, primaryBranchId);
                        foreach (var alert in alerts)
                        {
                            _logger.LogWarning("ALERT: {Type} - {Description}", alert.AlertType, alert.Description);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing OperationsReconciliationBackgroundService.");
                }

                // Run check every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
