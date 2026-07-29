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

        public LaneDisplayPublisherService(System.IServiceProvider serviceProvider, IHubContext<LaneDisplayHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
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

            // Firebase Update (Secondary for Mobile/Devices) - best-effort, never blocks SignalR
            try
            {
                if (FirebaseAdmin.FirebaseApp.DefaultInstance != null)
                {
                    var message = new Message()
                    {
                        Topic = $"branch-{eventDto.BranchId}-lane-display",
                        Data = new Dictionary<string, string>()
                        {
                            { "event", JsonSerializer.Serialize(eventDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) }
                        }
                    };
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
            }
            catch
            {
                // Firebase is optional secondary channel. Swallow silently.
            }
        }

        public async Task PublishClearAsync(int branchId, int? laneId, string? laneName)
        {
            var clearEvent = new LaneDisplayEventDTO
            {
                BranchId = branchId,
                Type = "cleared",
                LaneId = laneId,
                LaneName = laneName
            };

            await PublishEventAsync(clearEvent);
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

        public async Task PublishBarrierCommandAsync(int branchId, string licensePlate, string laneName)
        {
            var evt = new
            {
                Type = "BarrierOpenCommand",
                LicensePlate = licensePlate,
                LaneName = laneName,
                Timestamp = DateTime.UtcNow
            };

            // Firebase (Secondary for Edge devices) - best-effort
            try
            {
                if (FirebaseAdmin.FirebaseApp.DefaultInstance != null)
                {
                    var message = new Message()
                    {
                        Topic = $"branch-{branchId}-lane-display",
                        Data = new Dictionary<string, string>()
                        {
                            { "event", JsonSerializer.Serialize(evt, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) }
                        }
                    };
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
            }
            catch
            {
                // Firebase is optional secondary channel. Swallow silently.
            }
        }

        public async Task PublishBarrierCommandRawAsync(int branchId, string jsonPayload)
        {
            // Firebase (Secondary for Edge devices) - best-effort
            try
            {
                if (FirebaseAdmin.FirebaseApp.DefaultInstance != null)
                {
                    var message = new Message()
                    {
                        Topic = $"branch-{branchId}-lane-display",
                        Data = new Dictionary<string, string>()
                        {
                            { "event", jsonPayload }
                        }
                    };
                    await FirebaseMessaging.DefaultInstance.SendAsync(message);
                }
            }
            catch
            {
                // Firebase is optional secondary channel. Swallow silently.
            }
        }
    }
}
