using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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
                    var context = scope.ServiceProvider.GetRequiredService<AutoWashPro.DAL.Data.AutoWashDbContext>();

                    // Chạy reconciliation cho tất cả branch đang active
                    var activeBranchIds = await context.Branches
                        .Where(b => b.IsActive)
                        .Select(b => b.BranchId)
                        .ToListAsync(stoppingToken);

                    foreach (var branchId in activeBranchIds)
                    {
                        try
                        {
                            var alerts = await monitoringService.RunReconciliationCheckAsync(branchId, stoppingToken);
                            if (alerts.Count > 0)
                            {
                                _logger.LogWarning("Reconciliation check found {AlertCount} inconsistencies for Branch {BranchId}", alerts.Count, branchId);
                                foreach (var alert in alerts)
                                {
                                    _logger.LogWarning("ALERT [{Branch}]: {Type} - {Description}", branchId, alert.AlertType, alert.Description);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during reconciliation for Branch {BranchId}.", branchId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing OperationsReconciliationBackgroundService.");
                }

                // Chạy mỗi 60 giây
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}
