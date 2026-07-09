using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Helpers;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services
{
    public class LaneSchedulerService : ILaneSchedulerService
    {
        private readonly AutoWashDbContext _context;

        // Grace window past slot end before we reject the booking.
        // Absorbs real-world arrival variance without cascading into the next slot.
        private const int SlotGraceMinutes = 15;

        public LaneSchedulerService(AutoWashDbContext context)
        {
            _context = context;
        }

        // ── Public: projection ───────────────────────────────────────────────

        public async Task<Dictionary<int, DateTime>> GetLaneProjectedFreeTimesAsync(int branchId, DateTime slotStart)
        {
            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive)
                .ToListAsync();

            // All logs that are still in progress at this branch
            var activeLogs = await _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                    .ThenInclude(x => x.VehicleType)
                .Where(x =>
                    x.BranchId == branchId &&
                    (x.Status == "CheckedIn" ||
                     x.Status == "Assigned" ||
                     x.Status == "Processing"))
                .ToListAsync();

            var result = new Dictionary<int, DateTime>();

            foreach (var lane in lanes)
            {
                var logsOnLane = activeLogs
                    .Where(x => x.LaneId == lane.LaneId)
                    .OrderByDescending(x => x.CheckInTime)
                    .ToList();

                if (!logsOnLane.Any())
                {
                    // Lane is idle — free immediately from slot start
                    result[lane.LaneId] = slotStart;
                    continue;
                }

                // For the latest log on this lane, estimate when it finishes.
                // We derive duration from the vehicle's weight + the service prices
                // at that branch — no new DB columns needed.
                var latestLog = logsOnLane.First();
                var vehicleType = latestLog.FleetVehicle?.VehicleType;
                int baseWeight = vehicleType?.BaseWeight ?? 1;

                var servicePrices = await _context.ServicePrices
                    .Where(x =>
                        x.BranchId == branchId &&
                        x.VehicleTypeId == (vehicleType != null ? vehicleType.Id : 0))
                    .ToListAsync();

                int estimatedMinutes = WashTimeEstimator.EstimateMinutes(servicePrices);

                // ProjectedFreeAt = when the wash started + how long it takes
                // If already in Processing, use CheckInTime as the start baseline
                result[lane.LaneId] = latestLog.CheckInTime.AddMinutes(estimatedMinutes);
            }

            return result;
        }

        // ── Public: EAL simulation ───────────────────────────────────────────

        public async Task<LaneScheduleResult> ScheduleFleetAsync(int branchId, DateTime slotStart, TimeSpan slotDuration, List<VehicleScheduleRequest> vehicles)
        {
            if (!vehicles.Any())
                return LaneScheduleResult.Fail("Vehicle list cannot be empty.");

            var lanes = await _context.Lanes
                .Where(x =>
                    x.BranchId == branchId &&
                    x.IsActive &&
                    x.IsBusinessLane)
                .ToListAsync();

            if (!lanes.Any())
                return LaneScheduleResult.Fail("No available lane in this branch.");

            // ------------------------------------------------------------------
            // STEP 1: Current active occupancy from CheckedIn/Assigned/Processing
            // ------------------------------------------------------------------

            var projectedFreeTimes =
                await GetLaneProjectedFreeTimesAsync(branchId, slotStart);

            var laneQueue = lanes
                .Select(l => new LaneSimState
                {
                    LaneId = l.LaneId,
                    IsBusinessLane = l.IsBusinessLane,
                    FreeAt = projectedFreeTimes.TryGetValue(l.LaneId, out var t)
                        ? t
                        : slotStart
                })
                .ToList();

            // ------------------------------------------------------------------
            // STEP 2: Replay existing pending bookings already assigned
            // ------------------------------------------------------------------

            DateTime slotEnd = slotStart.Add(slotDuration);

            var existingBookings = await _context.Bookings
                .Include(x => x.BookingDetails)
                .Include(x => x.FleetVehicle)
                    .ThenInclude(x => x!.VehicleType)
                .Where(x =>
                    x.BranchId == branchId &&
                    x.BookingType == "Business" &&
                    x.Status == "Pending" &&
                    x.ProcessingLaneId != null &&
                    x.ScheduledTime >= slotStart &&
                    x.ScheduledTime < slotEnd)
                .OrderBy(x => x.BookingId)
                .ToListAsync();

            foreach (var booking in existingBookings)
            {
                var laneState = laneQueue.FirstOrDefault(x =>
                    x.LaneId == booking.ProcessingLaneId);

                if (laneState == null)
                    continue;

                var serviceIds = booking.BookingDetails
                    .Select(x => x.ServiceId)
                    .ToList();

                if (!serviceIds.Any())
                    continue;

                var servicePrices = await _context.ServicePrices
                    .Where(x =>
                        x.BranchId == branchId &&
                        x.VehicleTypeId == booking.FleetVehicle!.VehicleTypeId &&
                        serviceIds.Contains(x.ServiceId))
                    .ToListAsync();

                int washMinutes =
                    WashTimeEstimator.EstimateMinutes(servicePrices);

                DateTime estimatedStart =
                    laneState.FreeAt < slotStart
                        ? slotStart
                        : laneState.FreeAt;

                DateTime estimatedEnd =
                    estimatedStart.AddMinutes(washMinutes);

                laneState.FreeAt =
                    estimatedEnd.AddMinutes(
                        WashTimeEstimator.GetInterVehicleBuffer());
            }

            // ------------------------------------------------------------------
            // STEP 3: Schedule NEW vehicles
            // ------------------------------------------------------------------

            var assignments = new List<VehicleAssignment>();

            DateTime deadline =
                slotStart +
                slotDuration +
                TimeSpan.FromMinutes(SlotGraceMinutes);

            foreach (var vehicle in vehicles)
            {
                int washMinutes =
                    WashTimeEstimator.EstimateMinutes(vehicle.ServicePrices);

                laneQueue.Sort((a, b) => a.FreeAt.CompareTo(b.FreeAt));

                var chosenLane = laneQueue[0];

                DateTime estimatedStart =
                    chosenLane.FreeAt < slotStart
                        ? slotStart
                        : chosenLane.FreeAt;

                DateTime estimatedEnd =
                    estimatedStart.AddMinutes(washMinutes);

                if (estimatedEnd > deadline)
                {
                    return LaneScheduleResult.Fail(
                        $"Not enough time in the time slot for {vehicles.Count} vehicles. " +
                        $"Vehicle #{assignments.Count + 1} estimated completion time at " +
                        $"{estimatedEnd:HH:mm}, exceeding allowed limit ({deadline:HH:mm}).");
                }

                assignments.Add(new VehicleAssignment
                {
                    FleetVehicleId = vehicle.FleetVehicleId,
                    LaneId = chosenLane.LaneId,
                    EstimatedStart = estimatedStart,
                    EstimatedEnd = estimatedEnd
                });

                chosenLane.FreeAt =
                    estimatedEnd.AddMinutes(
                        WashTimeEstimator.GetInterVehicleBuffer());
            }

            return LaneScheduleResult.Ok(assignments);
        }
    }
}