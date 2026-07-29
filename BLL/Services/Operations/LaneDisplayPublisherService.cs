using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs.Operations;
using FirebaseAdmin.Messaging;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using AutoWashPro.BLL.Hubs;
using Microsoft.Extensions.Logging;

namespace AutoWashPro.BLL.Services.Operations
{
    public class LaneDisplayPublisherService : ILaneDisplayPublisherService
    {
        // BranchId -> (LaneId -> LatestState)
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, LaneDisplayLatestStateDTO>> _stateTracker
            = new ConcurrentDictionary<int, ConcurrentDictionary<int, LaneDisplayLatestStateDTO>>();

        // BranchId -> Latest Event
        private readonly ConcurrentDictionary<int, LaneDisplayEventDTO> _latestBranchEvent
            = new ConcurrentDictionary<int, LaneDisplayEventDTO>();

        private readonly System.IServiceProvider _serviceProvider;
        private readonly IHubContext<LaneDisplayHub> _hubContext;
        private readonly ILogger<LaneDisplayPublisherService> _logger;

        public LaneDisplayPublisherService(
            System.IServiceProvider serviceProvider,
            IHubContext<LaneDisplayHub> hubContext,
            ILogger<LaneDisplayPublisherService> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        private bool TryGetFirebaseApp(out FirebaseAdmin.FirebaseApp? app)
        {
            try
            {
                app = FirebaseAdmin.FirebaseApp.DefaultInstance;
                return app != null;
            }
            catch (InvalidOperationException)
            {
                app = null;
                return false;
            }
        }

        public async Task PublishEventAsync(LaneDisplayEventDTO eventDto)
        {
            _latestBranchEvent[eventDto.BranchId] = eventDto;

            if (eventDto.LaneId.HasValue)
            {
                var branchDict = _stateTracker.GetOrAdd(eventDto.BranchId, _ => new ConcurrentDictionary<int, LaneDisplayLatestStateDTO>());

                var latestState = new LaneDisplayLatestStateDTO
                {
                    LaneId = eventDto.LaneId.Value,
                    LaneName = eventDto.LaneName ?? $"Làn {eventDto.LaneId.Value}",
                    LatestEvent = eventDto
                };

                branchDict.AddOrUpdate(eventDto.LaneId.Value, latestState, (_, __) => latestState);
            }

            // SignalR Update (Primary for Web Display) - must always succeed
            await _hubContext.Clients
                .Group($"branch:{eventDto.BranchId}:lane-display")
                .SendAsync("ReceiveLaneUpdate", eventDto);

            _logger.LogDebug("SignalR ReceiveLaneUpdate published for BranchId={BranchId}, Type={Type}",
                eventDto.BranchId, eventDto.Type);

            // Firebase Update (Secondary for Mobile/Devices) - best-effort, never blocks SignalR
            try
            {
                if (!TryGetFirebaseApp(out var app))
                {
                    _logger.LogWarning(
                        "Firebase is not initialized. Skipping Firebase lane display event for BranchId={BranchId}, Type={Type}.",
                        eventDto.BranchId, eventDto.Type);
                    return;
                }

                var message = new Message()
                {
                    Topic = $"branch-{eventDto.BranchId}-lane-display",
                    Data = new Dictionary<string, string>()
                    {
                        { "event", JsonSerializer.Serialize(eventDto, OperationsOutboxEnvelope.OutboxJsonOptions) }
                    }
                };
                var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                if (string.IsNullOrWhiteSpace(messageId))
                {
                    _logger.LogWarning(
                        "Firebase returned empty messageId for BranchId={BranchId}, Type={Type}.",
                        eventDto.BranchId, eventDto.Type);
                }
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogWarning(ex,
                    "Firebase lane display publish failed for BranchId={BranchId}, Type={Type}. SignalR was already sent successfully.",
                    eventDto.BranchId, eventDto.Type);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Unexpected error in Firebase publish for BranchId={BranchId}. SignalR was already sent successfully.",
                    eventDto.BranchId);
            }
        }


        public async Task<LaneDisplayLatestResponseDTO> GetLatestStateAsync(int branchId)
        {
            var response = new LaneDisplayLatestResponseDTO
            {
                BranchId = branchId,
                ServerTime = DateTime.UtcNow
            };

            if (_latestBranchEvent.TryGetValue(branchId, out var latestEvent))
            {
                if (latestEvent.DisplayUntil == null || latestEvent.DisplayUntil >= DateTime.UtcNow)
                {
                    response.LatestEvent = latestEvent;
                }
            }

            if (_stateTracker.TryGetValue(branchId, out var branchDict) && !branchDict.IsEmpty)
            {
                response.Lanes = branchDict.Values.ToList();
                return response;
            }

            // Cache miss (e.g. app pool recycle). Reconstruct from database.
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(_serviceProvider);
            var context = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AutoWashPro.DAL.Data.AutoWashDbContext>(scope.ServiceProvider);

            var lanes = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                System.Linq.Queryable.Where(context.Lanes, l => l.BranchId == branchId && l.IsActive));

            var reconstructedDict = new ConcurrentDictionary<int, LaneDisplayLatestStateDTO>();

            foreach (var lane in lanes)
            {
                var activeBooking = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    System.Linq.Queryable.Where(context.Bookings, b => b.ProcessingLaneId == lane.LaneId && (b.Status == "CheckedIn" || b.Status == "Processing")));

                LaneDisplayEventDTO? evt = null;
                if (activeBooking != null)
                {
                    var vehicle = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                        System.Linq.Queryable.Where(context.Vehicles, v => v.Id == activeBooking.VehicleId));
                    evt = new LaneDisplayEventDTO
                    {
                        BranchId = branchId,
                        Type = activeBooking.Status == "CheckedIn" ? "assigned" : "processing",
                        BookingId = activeBooking.BookingId,
                        LicensePlate = vehicle?.LicensePlate,
                        LaneId = lane.LaneId,
                        LaneName = lane.Name
                    };
                }

                reconstructedDict[lane.LaneId] = new LaneDisplayLatestStateDTO
                {
                    LaneId = lane.LaneId,
                    LaneName = lane.Name,
                    LatestEvent = evt
                };
            }

            _stateTracker[branchId] = reconstructedDict;
            response.Lanes = reconstructedDict.Values.ToList();

            return response;
        }

        public async Task<BarrierPublishResult> PublishBarrierCommandAsync(int branchId, string licensePlate, string laneName)
        {
            // Firebase is REQUIRED for barrier commands (controls physical devices)
            if (!TryGetFirebaseApp(out var app))
            {
                _logger.LogWarning(
                    "Firebase is not initialized; barrier command cannot be published for BranchId={BranchId}. " +
                    "If testing locally, ignore this. In production, physical barrier will not open.", branchId);
                return BarrierPublishResult.SkippedNoFirebase; // Do NOT throw, otherwise outbox gets stuck.
            }

            var evt = new
            {
                Type = "BarrierOpenCommand",
                LicensePlate = licensePlate,
                LaneName = laneName,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var message = new Message()
                {
                    Topic = $"branch-{branchId}-lane-display",
                    Data = new Dictionary<string, string>()
                    {
                        { "event", JsonSerializer.Serialize(evt, OperationsOutboxEnvelope.OutboxJsonOptions) }
                    }
                };
                var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation(
                    "Barrier command published to Firebase for BranchId={BranchId}, Plate={Plate}, Lane={Lane}, MessageId={MessageId}",
                    branchId, licensePlate, laneName, messageId);
                return BarrierPublishResult.Published;
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex,
                    "Firebase barrier command publish FAILED for BranchId={BranchId}, Plate={Plate}. Outbox will retry.",
                    branchId, licensePlate);
                throw; // Re-throw so outbox marks it for retry
            }
        }

        public async Task<BarrierPublishResult> PublishBarrierCommandRawAsync(int branchId, string jsonPayload)
        {
            // Firebase is REQUIRED for barrier commands (controls physical devices)
            if (!TryGetFirebaseApp(out var app))
            {
                _logger.LogWarning(
                    "Firebase is not initialized; barrier command cannot be published for BranchId={BranchId}. " +
                    "If testing locally, ignore this. In production, physical barrier will not open.", branchId);
                return BarrierPublishResult.SkippedNoFirebase; // Do NOT throw, otherwise outbox gets stuck.
            }

            try
            {
                var message = new Message()
                {
                    Topic = $"branch-{branchId}-lane-display",
                    Data = new Dictionary<string, string>()
                    {
                        { "event", jsonPayload }
                    }
                };
                var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation(
                    "Barrier command raw published to Firebase for BranchId={BranchId}, MessageId={MessageId}",
                    branchId, messageId);
                return BarrierPublishResult.Published;
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogError(ex,
                    "Firebase barrier command raw publish FAILED for BranchId={BranchId}. Outbox will retry.",
                    branchId);
                throw; // Re-throw so outbox marks it for retry
            }
        }
    }
}
