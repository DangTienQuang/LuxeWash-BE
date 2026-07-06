using AutoWashPro.BLL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoWashPro.API.Workers
{
    public class AutoWashCompletionWorker : BackgroundService
    {
        private readonly ILogger<AutoWashCompletionWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AutoWashCompletionWorker(ILogger<AutoWashCompletionWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Auto Wash Completion Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                    await ProcessOverdueWashesAsync();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while running Auto Wash Completion Worker.");
                }
            }
        }

        private async Task ProcessOverdueWashesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                int completedCount = await bookingService.ProcessOverdueAutomatedWashesAsync();

                if (completedCount > 0)
                {
                    _logger.LogInformation("Auto Wash Completion Worker automatically completed {Count} overdue wash bookings.", completedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing overdue automated washes.");
            }
        }
    }
}
