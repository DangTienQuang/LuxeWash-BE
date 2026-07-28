using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Services.Operations;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoWashPro.BLL.BackgroundServices
{
    public class OutboxProcessorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessorService> _logger;

        public OutboxProcessorService(IServiceProvider serviceProvider, ILogger<OutboxProcessorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Outbox Processor Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages");
                }

                await Task.Delay(1000, stoppingToken); // Poll every second
            }
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<ILaneDisplayPublisherService>();

            var messages = await context.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.ErrorMessage == null)
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            if (!messages.Any()) return;

            foreach (var message in messages)
            {
                try
                {
                    if (message.Type == "LaneDisplayEvent")
                    {
                        var eventDto = JsonSerializer.Deserialize<LaneDisplayEventDTO>(message.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                        if (eventDto != null)
                        {
                            if (eventDto.Type == "cleared")
                            {
                                await publisher.PublishClearAsync(eventDto.BranchId, eventDto.LaneId.GetValueOrDefault(), eventDto.LaneName ?? "");
                            }
                            else
                            {
                                await publisher.PublishEventAsync(eventDto);
                            }
                        }
                    }
                    message.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process outbox message {message.Id}");
                    message.ErrorMessage = ex.Message;
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
