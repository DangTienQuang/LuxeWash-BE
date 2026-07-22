using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Hubs;

namespace AutoWashPro.BLL.Services.Operations
{
    public class LaneDisplayPublisherService : ILaneDisplayPublisherService
    {
        // BranchId -> (LaneId -> LatestState)
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, LaneDisplayLatestStateDTO>> _stateTracker
            = new ConcurrentDictionary<int, ConcurrentDictionary<int, LaneDisplayLatestStateDTO>>();

        private readonly IHubContext<LaneDisplayHub> _hubContext;

        private readonly System.IServiceProvider _serviceProvider;

        public LaneDisplayPublisherService(IHubContext<LaneDisplayHub> hubContext, System.IServiceProvider serviceProvider)
        {
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
        }

        public async Task PublishEventAsync(LaneDisplayEventDTO eventDto)
        {
            var branchDict = _stateTracker.GetOrAdd(eventDto.BranchId, _ => new ConcurrentDictionary<int, LaneDisplayLatestStateDTO>());

            var latestState = new LaneDisplayLatestStateDTO
            {
                LaneId = eventDto.LaneId,
                LaneName = eventDto.LaneName,
                LatestEvent = eventDto
            };

            branchDict.AddOrUpdate(eventDto.LaneId, latestState, (_, __) => latestState);

            await _hubContext.Clients.Group($"branch:{eventDto.BranchId}:lane-display")
                .SendAsync("ReceiveLaneUpdate", eventDto);
        }

        public async Task PublishClearAsync(int branchId, int laneId, string laneName)
        {
            var clearEvent = new LaneDisplayEventDTO
            {
                BranchId = branchId,
                Type = "Cleared",
                LaneId = laneId,
                LaneName = laneName
            };

            await PublishEventAsync(clearEvent);
        }

        public async Task<List<LaneDisplayLatestStateDTO>> GetLatestStateAsync(int branchId)
        {
            if (_stateTracker.TryGetValue(branchId, out var branchDict) && !branchDict.IsEmpty)
            {
                return branchDict.Values.ToList();
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
                        Type = activeBooking.Status == "CheckedIn" ? "Assigned" : "Processing",
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

            return reconstructedDict.Values.ToList();
        }
    }
}
