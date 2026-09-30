using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Helpers;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace BLL.Services
{
    public class LaneSchedulerService : ILaneSchedulerService
    {
        private readonly AutoWashDbContext _context;
        private readonly AutoWashPro.BLL.Services.Interface.IIncidentCapacityService _incidentCapacityService;
        public LaneSchedulerService(
            AutoWashDbContext context,
            AutoWashPro.BLL.Services.Interface.IIncidentCapacityService incidentCapacityService)
        {
            _context = context;
            _incidentCapacityService = incidentCapacityService;
        }
        public async Task<Dictionary<int, DateTime>> GetLaneProjectedFreeTimesAsync(int branchId, DateTime slotStart, bool isBusinessLane = false)
        {
            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive)
                .OrderBy(x => x.IsBusinessLane == isBusinessLane ? 0 : 1)
                .ThenBy(x => x.LaneId)
                .ToListAsync();
            var occupancies = await _context.LaneOccupancies
                .Include(o => o.Booking)
                    .ThenInclude(b => b!.BookingDetails)
                .Include(o => o.Booking)
                    .ThenInclude(b => b!.FleetVehicle)
                .Include(o => o.Booking)
                    .ThenInclude(b => b!.Vehicle)
                .Include(o => o.FleetWashLog)
                    .ThenInclude(f => f!.FleetVehicle)
                .Where(o => o.BranchId == branchId)
                .ToListAsync();

            var occupiedVehicleTypeIds = occupancies
                .Where(o => o.Booking != null && o.Booking.BookingDetails.Any())
                .Select(o => o.Booking!.FleetVehicle?.VehicleTypeId ?? o.Booking!.Vehicle?.VehicleTypeId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var servicePricesByVehicleType = (await _context.ServicePrices
                .Where(x => x.BranchId == branchId && occupiedVehicleTypeIds.Contains(x.VehicleTypeId))
                .ToListAsync())
                .GroupBy(x => x.VehicleTypeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new Dictionary<int, DateTime>();
            foreach (var lane in lanes)
            {
                var occupancy = occupancies.FirstOrDefault(o => o.LaneId == lane.LaneId);
                if (occupancy == null)
                {
                    result[lane.LaneId] = slotStart;
                }
                else
                {
                    var duration = 15;
                    if (occupancy.Booking != null && occupancy.Booking.BookingDetails.Any())
                    {
                        var vehicleTypeId = occupancy.Booking.FleetVehicle?.VehicleTypeId ?? occupancy.Booking.Vehicle?.VehicleTypeId;
                        if (vehicleTypeId != null)
                        {
                            var serviceIds = occupancy.Booking.BookingDetails.Select(x => x.ServiceId).ToList();
                            var prices = servicePricesByVehicleType.TryGetValue(vehicleTypeId.Value, out var priceList)
                                ? priceList.Where(x => serviceIds.Contains(x.ServiceId)).ToList()
                                : new List<AutoWashPro.DAL.Entities.ServicePrice>();
                            duration = WashTimeEstimator.EstimateMinutes(prices);
                        }
                    }
                    else if (occupancy.FleetWashLog != null && occupancy.FleetWashLog.FleetVehicle != null)
                    {
                        duration = 30; // Better fallback for fleet
                    }
                    
                    var projectedFree = occupancy.OccupiedAt.AddMinutes(duration);
                    result[lane.LaneId] = projectedFree > slotStart ? projectedFree : slotStart;
                }
            }
            return result;
        }
        public Task<int> GetBestAvailableLaneAsync(int branchId, bool isBusinessLane = false)
        {
            throw new NotSupportedException("Use ILaneAdmissionCoordinator for all realtime lane checking and assignment.");
        }
        public Task<int> AssignBestAvailableLaneAtomicAsync(int bookingId)
        {
            throw new NotSupportedException("Use ILaneAdmissionCoordinator.CheckInAtEntryGateAsync for realtime lane assignment.");
        }
        public Task<bool> AssignNextVehicleInQueueAsync(int laneId)
        {
            throw new NotSupportedException("Use ILaneAdmissionCoordinator.AdmitNextWaitingVehicleAsync for queue admission.");
        }
        private sealed class LaneReservation
        {
            public DateTime Start { get; init; }
            public DateTime EndWithBuffer { get; init; }
        }

        private sealed class ScheduleDaySnapshot
        {
            public List<AutoWashPro.DAL.Entities.TimeSlot> Slots { get; init; } = new();
            public List<AutoWashPro.DAL.Entities.Lane> Lanes { get; init; } = new();
            public Dictionary<int, DateTime> ProjectedFreeTimes { get; init; } = new();
            public List<AutoWashPro.DAL.Entities.Booking> ExistingBookings { get; init; } = new();
            public Dictionary<int, List<AutoWashPro.DAL.Entities.ServicePrice>> ExistingPricesByVehicleType { get; init; } = new();
            public Dictionary<int, int> BookedWeights { get; init; } = new();
            public bool HasActiveIncident { get; init; }
        }

        // LaneSchedulerService is scoped to one HTTP request. Availability and
        // incident-option endpoints evaluate many starting slots in that request;
        // cache the shared branch/day data so those evaluations do not repeat the
        // same database queries for every slot.
        private readonly Dictionary<(int BranchId, DateTime Date), Task<ScheduleDaySnapshot>> _daySnapshots = new();

        private Task<ScheduleDaySnapshot> GetDaySnapshotAsync(int branchId, DateTime targetDate)
        {
            var key = (branchId, targetDate.Date);
            if (!_daySnapshots.TryGetValue(key, out var snapshotTask))
            {
                snapshotTask = LoadDaySnapshotAsync(branchId, targetDate.Date);
                _daySnapshots[key] = snapshotTask;
            }

            return snapshotTask;
        }

        private async Task<ScheduleDaySnapshot> LoadDaySnapshotAsync(int branchId, DateTime dayStart)
        {
            var dayEnd = dayStart.AddDays(1);
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var slots = await _context.TimeSlots
                .Where(x => x.BranchId == branchId)
                .OrderBy(x => x.StartTime)
                .ToListAsync();
            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive)
                .OrderBy(x => x.IsBusinessLane ? 0 : 1)
                .ThenBy(x => x.LaneId)
                .ToListAsync();
            var projectedFreeTimes = await GetLaneProjectedFreeTimesAsync(
                branchId,
                dayStart,
                isBusinessLane: true);
            var existingBookings = await _context.Bookings
                .Include(x => x.BookingDetails)
                .Include(x => x.FleetVehicle)
                .Include(x => x.Vehicle)
                .Where(x =>
                    x.BranchId == branchId &&
                    (x.BookingType == "Business" || x.BookingType == "Fleet") &&
                    (x.Status == "Pending" || x.Status == "Confirmed") &&
                    x.ScheduledTime >= dayStart &&
                    x.ScheduledTime < dayEnd)
                .OrderBy(x => x.ScheduledTime)
                .ThenBy(x => x.BookingId)
                .ToListAsync();
            var vehicleTypeIds = existingBookings
                .Where(b => b.BookingDetails.Count > 0)
                .Select(b => b.FleetVehicle?.VehicleTypeId ?? b.Vehicle?.VehicleTypeId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var pricesByVehicleType = (await _context.ServicePrices
                .Where(x => x.BranchId == branchId && vehicleTypeIds.Contains(x.VehicleTypeId))
                .ToListAsync())
                .GroupBy(x => x.VehicleTypeId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var bookedWeights = await _context.DailySlotCapacities
                .Where(x => x.BranchId == branchId && x.Date == dayStart)
                .ToDictionaryAsync(x => x.SlotId, x => x.BookedWeight);
            var hasActiveIncident = await _context.BranchIncidents.AnyAsync(i =>
                i.BranchId == branchId &&
                i.Status == "Active" &&
                i.StartedAtVn < dayEnd &&
                (i.EstimatedEndAtVn > dayStart || i.EstimatedEndAtVn <= now));

            return new ScheduleDaySnapshot
            {
                Slots = slots,
                Lanes = lanes,
                ProjectedFreeTimes = projectedFreeTimes,
                ExistingBookings = existingBookings,
                ExistingPricesByVehicleType = pricesByVehicleType,
                BookedWeights = bookedWeights,
                HasActiveIncident = hasActiveIncident
            };
        }

        private static DateTime? FindEarliestStart(
            DateTime windowStart,
            DateTime windowEnd,
            int durationMinutes,
            DateTime minimumStart,
            List<LaneReservation> reservations)
        {
            var candidate = windowStart > minimumStart ? windowStart : minimumStart;
            foreach (var reservation in reservations.OrderBy(x => x.Start))
            {
                if (reservation.EndWithBuffer <= candidate)
                    continue;
                if (candidate.AddMinutes(durationMinutes) <= reservation.Start)
                    return candidate;
                candidate = reservation.EndWithBuffer;
            }

            return candidate.AddMinutes(durationMinutes) <= windowEnd
                ? candidate
                : null;
        }

        public async Task<LaneScheduleResult> ScheduleFleetAcrossSlotsAsync(
            int branchId,
            DateTime targetDate,
            int startingSlotId,
            List<VehicleScheduleRequest> vehicles,
            int? excludedBookingId = null)
        {
            if (!vehicles.Any())
                return LaneScheduleResult.Fail("Vehicle list cannot be empty.");

            var dayStart = targetDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var snapshot = await GetDaySnapshotAsync(branchId, dayStart);
            var allSlots = snapshot.Slots;
            var startingIndex = allSlots.FindIndex(x => x.SlotId == startingSlotId);
            if (startingIndex < 0)
                return LaneScheduleResult.Fail("Time slot not found.");
            var candidateSlots = allSlots.Skip(startingIndex).ToList();

            var lanes = snapshot.Lanes;
            if (!lanes.Any())
                return LaneScheduleResult.Fail("No available lane in this branch.");

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var incidentIsActive = snapshot.HasActiveIncident;
            var minimumLaneStart = lanes.ToDictionary(
                lane => lane.LaneId,
                lane => snapshot.ProjectedFreeTimes.TryGetValue(lane.LaneId, out var freeAt)
                    ? freeAt
                    : dayStart);
            var reservations = lanes.ToDictionary(
                lane => lane.LaneId,
                _ => new List<LaneReservation>());

            foreach (var booking in snapshot.ExistingBookings.Where(x => x.BookingId != excludedBookingId))
            {
                if (booking.BookingDetails.Count == 0)
                    continue;
                var vehicleTypeId = booking.FleetVehicle?.VehicleTypeId ?? booking.Vehicle?.VehicleTypeId;
                if (vehicleTypeId == null)
                    continue;

                var serviceIds = booking.BookingDetails.Select(x => x.ServiceId).ToList();
                var prices = snapshot.ExistingPricesByVehicleType.TryGetValue(vehicleTypeId.Value, out var priceList)
                    ? priceList.Where(x => serviceIds.Contains(x.ServiceId)).ToList()
                    : new List<AutoWashPro.DAL.Entities.ServicePrice>();
                var duration = WashTimeEstimator.EstimateMinutes(prices);
                var existingChoice = lanes
                    .Select(lane => new
                    {
                        lane.LaneId,
                        Start = FindEarliestStart(
                            booking.ScheduledTime,
                            dayEnd,
                            duration,
                            minimumLaneStart[lane.LaneId],
                            reservations[lane.LaneId])
                    })
                    .Where(x => x.Start.HasValue)
                    .OrderBy(x => x.Start)
                    .ThenBy(x => x.LaneId)
                    .FirstOrDefault();
                if (existingChoice?.Start == null)
                    continue;
                reservations[existingChoice.LaneId].Add(new LaneReservation
                {
                    Start = existingChoice.Start.Value,
                    EndWithBuffer = existingChoice.Start.Value
                        .AddMinutes(duration + WashTimeEstimator.GetInterVehicleBuffer())
                });
            }

            var bookedWeights = new Dictionary<int, int>(snapshot.BookedWeights);
            var excludedWeightsBySlot = new Dictionary<int, int>();
            if (excludedBookingId.HasValue)
            {
                var excludedBooking = snapshot.ExistingBookings
                    .FirstOrDefault(x => x.BookingId == excludedBookingId.Value);
                if (excludedBooking != null &&
                    excludedBooking.BranchId == branchId &&
                    excludedBooking.ScheduledTime.Date == dayStart)
                {
                    var excludedSlot = allSlots.FirstOrDefault(x =>
                        x.StartTime <= excludedBooking.ScheduledTime.TimeOfDay &&
                        x.EndTime > excludedBooking.ScheduledTime.TimeOfDay);
                    if (excludedSlot != null && bookedWeights.ContainsKey(excludedSlot.SlotId))
                    {
                        var excludedCapacityWeight = excludedBooking.CapacityWeight > 0
                            ? excludedBooking.CapacityWeight
                            : 1;
                        excludedWeightsBySlot[excludedSlot.SlotId] = excludedCapacityWeight;
                        bookedWeights[excludedSlot.SlotId] = Math.Max(
                            0,
                            bookedWeights[excludedSlot.SlotId] - excludedCapacityWeight);
                    }
                }
            }

            var assignments = new List<VehicleAssignment>();
            var newlyAssignedWeights = new Dictionary<int, int>();
            var incidentCapacities = new Dictionary<int, AutoWashPro.BLL.Services.Interface.EffectiveSlotCapacityResult>();
            foreach (var vehicle in vehicles)
            {
                var duration = WashTimeEstimator.EstimateMinutes(vehicle.ServicePrices);
                VehicleAssignment? assignment = null;
                foreach (var slot in candidateSlots)
                {
                    AutoWashPro.BLL.Services.Interface.EffectiveSlotCapacityResult incidentCapacity;
                    if (!incidentIsActive)
                    {
                        var slotBookedWeight = bookedWeights.TryGetValue(slot.SlotId, out var currentWeight)
                            ? currentWeight
                            : 0;
                        incidentCapacity = new AutoWashPro.BLL.Services.Interface.EffectiveSlotCapacityResult
                        {
                            BaseCapacity = slot.MaxCapacity,
                            EffectiveCapacity = slot.MaxCapacity,
                            BookedWeight = slotBookedWeight,
                            AvailableWeight = Math.Max(0, slot.MaxCapacity - slotBookedWeight)
                        };
                    }
                    else if (!incidentCapacities.TryGetValue(slot.SlotId, out incidentCapacity!))
                    {
                        incidentCapacity = await _incidentCapacityService.GetEffectiveSlotCapacityAsync(
                            branchId,
                            targetDate,
                            slot.SlotId,
                            new AutoWashPro.BLL.Services.Interface.BookingContextDTO
                            {
                                IsBusiness = true,
                                IsVipEligible = false,
                                VehicleTypeId = vehicle.VehicleType.Id,
                                ServiceIds = vehicle.ServicePrices.Select(p => p.ServiceId).ToList(),
                                CapacityWeight = vehicle.CapacityWeight
                            },
                            now);
                        incidentCapacities[slot.SlotId] = incidentCapacity;
                    }

                    var excludedWeight = excludedWeightsBySlot.TryGetValue(slot.SlotId, out var ownWeight)
                        ? ownWeight
                        : 0;
                    var bookedWeight = Math.Max(0, incidentCapacity.BookedWeight - excludedWeight) +
                        (newlyAssignedWeights.TryGetValue(slot.SlotId, out var assignedWeight)
                            ? assignedWeight
                            : 0);
                    if (bookedWeight + vehicle.CapacityWeight > incidentCapacity.EffectiveCapacity)
                        continue;

                    var slotStart = dayStart.Add(slot.StartTime);
                    var slotEnd = dayStart.Add(slot.EndTime);
                    var eligibleLaneIds = incidentCapacity.IncidentIds.Count > 0
                        ? incidentCapacity.EligibleLaneIds.ToHashSet()
                        : null;
                    var laneChoice = lanes
                        .Where(lane => eligibleLaneIds == null || eligibleLaneIds.Contains(lane.LaneId))
                        .Select(lane => new
                        {
                            lane.LaneId,
                            Start = FindEarliestStart(
                                slotStart,
                                slotEnd,
                                duration,
                                minimumLaneStart[lane.LaneId],
                                reservations[lane.LaneId])
                        })
                        .Where(x => x.Start.HasValue)
                        .OrderBy(x => x.Start)
                        .ThenBy(x => x.LaneId)
                        .FirstOrDefault();
                    if (laneChoice?.Start == null)
                        continue;

                    var estimatedStart = laneChoice.Start.Value;
                    var estimatedEnd = estimatedStart.AddMinutes(duration);
                    assignment = new VehicleAssignment
                    {
                        FleetVehicleId = vehicle.FleetVehicleId,
                        AssignedSlotId = slot.SlotId,
                        LaneId = laneChoice.LaneId,
                        EstimatedStart = estimatedStart,
                        EstimatedEnd = estimatedEnd
                    };
                    reservations[laneChoice.LaneId].Add(new LaneReservation
                    {
                        Start = estimatedStart,
                        EndWithBuffer = estimatedEnd.AddMinutes(WashTimeEstimator.GetInterVehicleBuffer())
                    });
                    newlyAssignedWeights[slot.SlotId] =
                        (newlyAssignedWeights.TryGetValue(slot.SlotId, out var currentAssignedWeight)
                            ? currentAssignedWeight
                            : 0) + vehicle.CapacityWeight;
                    break;
                }

                if (assignment == null)
                {
                    return LaneScheduleResult.Fail(
                        $"Không đủ sức chứa từ khung giờ đã chọn đến cuối ngày cho {vehicles.Count} xe.");
                }
                assignments.Add(assignment);
            }

            return LaneScheduleResult.Ok(assignments);
        }
    }
}
