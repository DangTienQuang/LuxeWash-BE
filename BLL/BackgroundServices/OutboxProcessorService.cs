#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
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
                        await Task.Delay(100, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(3000, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // App đang shutdown – thoát im lặng
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox messages");
                    try
                    {
                        await Task.Delay(3000, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _logger.LogInformation("Outbox Processor Service stopped.");
        }

        private async Task<bool> ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<ILaneDisplayPublisherService>();

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var messages = await context.OutboxMessages
                .Where(m => m.ProcessedAt == null && (m.NextRetryAt == null || m.NextRetryAt <= now) && (m.ErrorMessage == null || m.RetryCount < 3))
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
                            var envelope = JsonSerializer.Deserialize<OperationsOutboxEnvelope>(message.Payload, OperationsOutboxEnvelope.OutboxJsonOptions);
                            
                            if (envelope == null || envelope.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                            {
                                throw new InvalidOperationException($"Invalid outbox envelope. MessageId={message.Id}, Type={message.Type}");
                            }

                            var eventDto = envelope.Data.Deserialize<LaneDisplayEventDTO>(OperationsOutboxEnvelope.OutboxJsonOptions);
                            if (eventDto != null)
                            {
                                eventDto.Type = envelope.Type switch {
                                    "admission_granted" => "assigned",
                                    "vehicle_waiting" => "waiting",
                                    "lane_cleared" => "cleared",
                                    _ => envelope.Type
                                };
                                eventDto.BranchId = envelope.BranchId;
                                eventDto.EventId = envelope.EventId;
                                eventDto.OccurredAt = envelope.OccurredAt;

                                if (!string.IsNullOrEmpty(eventDto.BarrierCommandId))
                                {
                                    var barrierCmd = await context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == eventDto.BarrierCommandId);
                                    if (barrierCmd != null)
                                    {
                                        if (barrierCmd.Status == "Pending")
                                        {
                                            // Delay display event until barrier is published
                                            break;
                                        }
                                        eventDto.BarrierStatus = barrierCmd.Status;
                                    }
                                }

                                if (eventDto.Type == "cleared")
                                {
                                    // Use PublishEventAsync instead of PublishClearAsync
                                    await publisher.PublishEventAsync(eventDto);
                                }
                                else
                                {
                                    await publisher.PublishEventAsync(eventDto);
                                }
                            }
                            message.ProcessedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                            break;

                        case "barrier_command":
                            var barrierEnvelope = JsonSerializer.Deserialize<OperationsOutboxEnvelope>(message.Payload, OperationsOutboxEnvelope.OutboxJsonOptions);
                            
                            if (barrierEnvelope == null || barrierEnvelope.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                            {
                                throw new InvalidOperationException($"Invalid barrier outbox envelope. MessageId={message.Id}, Type={message.Type}");
                            }

                            var cmdIdElement = barrierEnvelope.Data.GetProperty("commandId");
                            if (cmdIdElement.ValueKind == JsonValueKind.String)
                            {
                                var cmdIdStr = cmdIdElement.GetString();
                                var bCmd = await context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == cmdIdStr);
                                if (bCmd != null)
                                {
                                    if (bCmd.Status != "Pending" || bCmd.ExpiresAt <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow)
                                    {
                                        if (bCmd.Status == "Pending" && bCmd.ExpiresAt <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow)
                                        {
                                            bCmd.Status = "Expired";
                                        }
                                        
                                        message.ProcessedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                                        message.NextRetryAt = null;
                                        message.ErrorMessage = null;
                                        break; // Do not send to Firebase
                                    }
                                }
                            }

                            // We publish the full envelope to Firebase
                            var jsonPayload = JsonSerializer.Serialize(barrierEnvelope, OperationsOutboxEnvelope.OutboxJsonOptions);
                            var publishResult = await publisher.PublishBarrierCommandRawAsync(barrierEnvelope.BranchId, jsonPayload);

                            if (publishResult == BarrierPublishResult.SkippedNoFirebase)
                            {
                                message.ProcessedAt = null;
                                message.NextRetryAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddSeconds(30);
                                message.ErrorMessage = "Firebase is not available.";
                                break;
                            }

                            // Update BarrierCommand status to Published only if actually published
                            if (publishResult == BarrierPublishResult.Published)
                            {
                                if (cmdIdElement.ValueKind == JsonValueKind.String)
                                {
                                    var commandId = cmdIdElement.GetString();
                                    var barrierCmd = await context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == commandId);
                                    if (barrierCmd != null && barrierCmd.Status == "Pending")
                                    {
                                        barrierCmd.Status = "Published";
                                    }
                                }
                            }
                            
                            message.ProcessedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                            message.RetryCount = 0;
                            message.NextRetryAt = null;
                            message.ErrorMessage = null;
                            break;
                            
                        case "LaneDisplayEvent":
                            // Legacy format processing
                            var legacyDto = JsonSerializer.Deserialize<LaneDisplayEventDTO>(message.Payload, OperationsOutboxEnvelope.OutboxJsonOptions);
                            if (legacyDto != null)
                            {
                                if (!string.IsNullOrEmpty(legacyDto.BarrierCommandId))
                                {
                                    var barrierCmd = await context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == legacyDto.BarrierCommandId);
                                    if (barrierCmd != null)
                                    {
                                        if (barrierCmd.Status == "Pending")
                                        {
                                            // Delay display event until barrier is published
                                            break;
                                        }
                                        legacyDto.BarrierStatus = barrierCmd.Status;
                                    }
                                }
                                if (legacyDto.Type == "cleared")
                                {
                                    await publisher.PublishEventAsync(legacyDto);
                                }
                                else
                                {
                                    await publisher.PublishEventAsync(legacyDto);
                                }
                            }
                            message.ProcessedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                            break;

                        default:
                            _logger.LogWarning($"Unsupported outbox message type: {message.Type}. Skipping without marking as processed.");
                            // We do NOT set ProcessedAt for unknown types, but we set an error so we don't retry infinitely
                            message.ErrorMessage = "Unsupported outbox message type";
                            break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // App đang shutdown – không tăng RetryCount, để message được xử lý lần sau
                    throw; // Re-throw để vòng lặp ngoài bắt và thoát sạch
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to process outbox message {message.Id} (Type={message.Type})");
                    
                    message.RetryCount++;
                    message.ErrorMessage = ex.ToString();

                    if (message.RetryCount >= 3)
                    {
                        message.ErrorMessage = $"Failed permanently after 3 retries. Last error:\n{ex}";
                    }
                    else
                    {
                        var delaySeconds = message.RetryCount switch
                        {
                            1 => 1,
                            2 => 3,
                            _ => 10
                        };
                        message.NextRetryAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddSeconds(delaySeconds);
                    }
                }
            }

            // Dùng CancellationToken.None để SaveChanges không bị cancel giữa chừng khi app shutdown
            await context.SaveChangesAsync(CancellationToken.None);
            return true;
        }
    }
}

#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
