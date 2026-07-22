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

        public async Task<Dictionary<int, DateTime>> GetLaneProjectedFreeTimesAsync(int branchId, DateTime slotStart, bool isBusinessLane = false)
        {
            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive && x.IsBusinessLane == isBusinessLane)
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

            var activeBookings = await _context.Bookings
                .Include(x => x.BookingDetails)
                .Include(x => x.ActualVehicleType)
                .Where(x =>
                    x.BranchId == branchId &&
                    x.ProcessingLaneId != null &&
                    (x.Status == "CheckedIn" || x.Status == "Processing" || x.Status == "Pending"))
                .ToListAsync();

            var result = new Dictionary<int, DateTime>();

            foreach (var lane in lanes)
            {
                var logsOnLane = activeLogs
                    .Where(x => x.LaneId == lane.LaneId)
                    .OrderByDescending(x => x.CheckInTime)
                    .ToList();

                var bookingsOnLane = activeBookings
                    .Where(x => x.ProcessingLaneId == lane.LaneId)
                    .OrderByDescending(x => x.ProcessingStartTime ?? x.ScheduledTime)
                    .ToList();

                if (!logsOnLane.Any() && !bookingsOnLane.Any())
                {
                    // Lane is idle — free immediately from slot start
                    result[lane.LaneId] = slotStart;
                    continue;
                }

                DateTime projectedFreeAt = slotStart;

                if (logsOnLane.Any())
                {
                    var latestLog = logsOnLane.First();
                    // Hardcode max lane occupancy to 15 mins since detailing is done outside
                    int estimatedMinutes = 15;
                    var logProjectedFreeAt = latestLog.CheckInTime.AddMinutes(estimatedMinutes);
                    if (logProjectedFreeAt > projectedFreeAt) projectedFreeAt = logProjectedFreeAt;
                }

                if (bookingsOnLane.Any())
                {
                    var latestBooking = bookingsOnLane.First();
                    // Hardcode max lane occupancy to 15 mins since detailing is done outside
                    int estimatedMinutes = 15;
                    var baseTime = latestBooking.ProcessingStartTime ?? latestBooking.UpdatedAt ?? DateTime.UtcNow;
                    // Fallback to now if baseTime is too old/invalid
                    if (baseTime < DateTime.UtcNow.AddDays(-1)) baseTime = DateTime.UtcNow;

                    var bookingProjectedFreeAt = baseTime.AddMinutes(estimatedMinutes);
                    if (bookingProjectedFreeAt > projectedFreeAt) projectedFreeAt = bookingProjectedFreeAt;
                }

                result[lane.LaneId] = projectedFreeAt;
            }

            return result;
        }

        public async Task<int> GetBestAvailableLaneAsync(int branchId, bool isBusinessLane = false)
        {
            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive && x.IsBusinessLane == isBusinessLane)
                .ToListAsync();

            if (!lanes.Any())
            {
                // Fallback to any active lane if no lane matches the exact business requirement
                lanes = await _context.Lanes
                    .Where(x => x.BranchId == branchId && x.IsActive)
                    .ToListAsync();
                
                if (!lanes.Any()) return 0;
            }

            var projectedFreeTimes = await GetLaneProjectedFreeTimesAsync(branchId, DateTime.UtcNow, isBusinessLane);

            // If projected free times dictionary doesn't have a lane (due to fallback above), add it with current time
            foreach(var lane in lanes)
            {
                if (!projectedFreeTimes.ContainsKey(lane.LaneId))
                {
                    projectedFreeTimes[lane.LaneId] = DateTime.UtcNow;
                }
            }

            // Find lanes that are currently free (or free within a tiny margin, e.g. 1 min)
            var freeLanes = lanes
                .Where(l => projectedFreeTimes[l.LaneId] <= DateTime.UtcNow.AddMinutes(1))
                .ToList();

            if (!freeLanes.Any())
            {
                return 0; // No lanes are currently free, must enter Waiting Queue
            }

            // Find the free lane with the earliest projected free time (usually all are in the past/now)
            var bestLaneId = freeLanes
                .Select(l => l.LaneId)
                .OrderBy(id => projectedFreeTimes[id])
                .First();

            return bestLaneId;
        }

        public async Task<int> AssignBestAvailableLaneAtomicAsync(int bookingId)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId);
                if (booking == null || booking.ProcessingLaneId != null) 
                    return booking?.ProcessingLaneId ?? 0;

                int laneId = await GetBestAvailableLaneAsync(booking.BranchId, booking.BookingType == "Business");
                if (laneId > 0)
                {
                    booking.ProcessingLaneId = laneId;
                    await _context.SaveChangesAsync();
                }

                await dbTransaction.CommitAsync();
                return laneId;
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> AssignNextVehicleInQueueAsync(int laneId)
        {
            var lane = await _context.Lanes.FindAsync(laneId);
            if (lane == null) return false;

            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    using var dbTransaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                    // Query all pending bookings that are checked in at this branch but waiting for a lane
                    var waitingBookings = await _context.Bookings
                        .Include(b => b.User)
                            .ThenInclude(u => u.CustomerProfile)
                                .ThenInclude(cp => cp.Tier)
                        .Where(b => b.BranchId == lane.BranchId && b.Status == "CheckedIn" && b.ProcessingLaneId == null)
                        .ToListAsync();

                    var paidOrFreeBookings = new List<AutoWashPro.DAL.Entities.Booking>();
                    foreach (var b in waitingBookings)
                    {
                        var isPaid = await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, b);
                        if (isPaid)
                        {
                            paidOrFreeBookings.Add(b);
                        }
                    }

                    if (!paidOrFreeBookings.Any()) return false;

                    // Sort queue:
                    // 1. Scheduled (Non-WalkIn) > WalkIn
                    // 2. High Tier > Low Tier
                    // 3. Earliest Update Time
                    var nextBooking = paidOrFreeBookings
                        .OrderByDescending(b => b.BookingType != "WalkIn")
                        .ThenByDescending(b => b.User?.CustomerProfile?.Tier?.MinAccumulatedPoints ?? 0)
                        .ThenBy(b => b.UpdatedAt)
                        .First();

                    if (nextBooking.ProcessingLaneId == null)
                    {
                        nextBooking.ProcessingLaneId = laneId;
                        nextBooking.ProcessingStaffId = null; // Can be assigned later by staff
                        nextBooking.UpdatedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync();
                        await dbTransaction.CommitAsync();
                        return true;
                    }
                    
                    return false;
                }
                catch (DbUpdateConcurrencyException)
                {
                    retryCount--;
                    if (retryCount == 0) throw;
                    await Task.Delay(50);
                }
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1213 || ex.Number == 1205) // Deadlock or Lock wait timeout
                {
                    retryCount--;
                    if (retryCount == 0) throw;
                    await Task.Delay(50);
                }
            }
            
            return false;
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