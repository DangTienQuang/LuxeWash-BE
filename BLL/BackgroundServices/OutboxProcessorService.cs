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
                    bool hasMessages = await ProcessOutboxMessagesAsync(stoppingToken);
                    if (hasMessages)
                    {
                        await Task.Delay(100, stoppingToken); // Fast polling if there are still messages
                    }
                    else
                    {
                        await Task.Delay(3000, stoppingToken); // Sleep for 3 seconds if no messages
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages");
                    await Task.Delay(3000, stoppingToken);
                }
            }
        }

        private async Task<bool> ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<ILaneDisplayPublisherService>();

            var messages = await context.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.ErrorMessage == null)
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            if (!messages.Any()) return false;

            foreach (var message in messages)
            {
                try
                {
                    switch (message.Type)
                    {
                        case "vehicle_waiting":
                        case "admission_granted":
                        case "lane_cleared":
                        case "assigned": // Backward compatibility if someone used 'assigned'
                            var envelope = JsonSerializer.Deserialize<OperationsOutboxEnvelope>(message.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                            if (envelope != null)
                            {
                                var eventDto = envelope.Data.Deserialize<LaneDisplayEventDTO>(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                if (eventDto != null)
                                {
                                    eventDto.Type = envelope.Type;
                                    eventDto.BranchId = envelope.BranchId;
                                    eventDto.EventId = envelope.EventId;
                                    eventDto.OccurredAt = envelope.OccurredAt;

                                    if (eventDto.Type == "lane_cleared" || eventDto.Type == "cleared")
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
                            break;

                        case "barrier_command":
                            var barrierEnvelope = JsonSerializer.Deserialize<OperationsOutboxEnvelope>(message.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                            if (barrierEnvelope != null)
                            {
                                // We publish the full envelope to Firebase
                                var jsonPayload = JsonSerializer.Serialize(barrierEnvelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                await publisher.PublishBarrierCommandRawAsync(barrierEnvelope.BranchId, jsonPayload);

                                // Update BarrierCommand status to Published
                                var commandIdElement = barrierEnvelope.Data.GetProperty("commandId");
                                if (commandIdElement.ValueKind == JsonValueKind.String)
                                {
                                    var commandId = commandIdElement.GetString();
                                    var barrierCmd = await context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == commandId);
                                    if (barrierCmd != null && barrierCmd.Status == "Pending")
                                    {
                                        barrierCmd.Status = "Published";
                                    }
                                }
                            }
                            message.ProcessedAt = DateTime.UtcNow;
                            break;
                            
                        case "LaneDisplayEvent":
                            // Legacy format processing
                            var legacyDto = JsonSerializer.Deserialize<LaneDisplayEventDTO>(message.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                            if (legacyDto != null)
                            {
                                if (legacyDto.Type == "cleared")
                                {
                                    await publisher.PublishClearAsync(legacyDto.BranchId, legacyDto.LaneId.GetValueOrDefault(), legacyDto.LaneName ?? "");
                                }
                                else
                                {
                                    await publisher.PublishEventAsync(legacyDto);
                                }
                            }
                            message.ProcessedAt = DateTime.UtcNow;
                            break;

                        default:
                            _logger.LogWarning($"Unsupported outbox message type: {message.Type}. Skipping without marking as processed.");
                            // We do NOT set ProcessedAt for unknown types, but we set an error so we don't retry infinitely
                            message.ErrorMessage = "Unsupported outbox message type";
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process outbox message {message.Id}");
                    message.ErrorMessage = ex.Message;
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            return true;
        }
    }
}
