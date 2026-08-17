using AutoWashPro.BLL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoWashPro.API.Workers
{
    public class CRMCampaignWorker : BackgroundService
    {
        private readonly ILogger<CRMCampaignWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public CRMCampaignWorker(ILogger<CRMCampaignWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CRM Campaign Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextVnMidnight();
                _logger.LogInformation("Next CRM Campaign scan scheduled in {Delay}.", delay);

                await Task.Delay(delay, stoppingToken);
                await ProcessDailyCampaignsAsync();
            }
        }

        private async Task ProcessDailyCampaignsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var crmCampaignService = scope.ServiceProvider.GetRequiredService<ICRMCampaignService>();
                var results = await crmCampaignService.ProcessDailyCampaignsAsync();

                foreach (var result in results)
                {
                    _logger.LogInformation(
                        "Processed voucher campaign {CampaignType}/{Code}: scanned={Scanned}, granted={Granted}, skipped={Skipped}",
                        result.CampaignType,
                        result.VoucherCode,
                        result.ScannedUsers,
                        result.GrantedCount,
                        result.SkippedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing daily voucher campaigns.");
            }
        }

        private static TimeSpan GetDelayUntilNextVnMidnight()
        {
            var vnTimeZone = GetVnTimeZone();
            var nowUtc = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var nowVn = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, vnTimeZone);
            var nextVnMidnight = nowVn.Date.AddDays(1);
            var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextVnMidnight, vnTimeZone);

            return nextUtc - nowUtc;
        }

        private static TimeZoneInfo GetVnTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
        }
    }
}
