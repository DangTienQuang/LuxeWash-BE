using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AutoWashPro.BLL.Services;

namespace AutoWashPro.API.Workers
{
    public class AnnualTierResetWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public AnnualTierResetWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;

                // Chạy vào 23:59 ngày 31/12
                if (now.Month == 12 && now.Day == 31 && now.Hour == 23)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var annualTierService = scope.ServiceProvider.GetRequiredService<IAnnualTierService>();

                    await annualTierService.ResetAnnualTiersAsync(stoppingToken);

                    // Ngủ 24h để tránh chạy lại liên tục trong cùng ngày 31/12
                    await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                }

                // Kiểm tra mỗi giờ 1 lần
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
