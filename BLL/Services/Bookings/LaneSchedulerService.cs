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
        public LaneSchedulerService(AutoWashDbContext context)
        {
            _context = context;
        }
        public async Task<Dictionary<int, DateTime>> GetLaneProjectedFreeTimesAsync(int branchId, DateTime slotStart, bool isBusinessLane = false)
        {
            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive && x.IsBusinessLane == isBusinessLane)
                .ToListAsync();
            var occupancies = await _context.LaneOccupancies
                .Where(o => o.BranchId == branchId)
                .ToListAsync();
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
                    var projectedFree = occupancy.OccupiedAt.AddMinutes(15);
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

            var allSlots = await _context.TimeSlots
                .Where(x => x.BranchId == branchId)
                .OrderBy(x => x.StartTime)
                .ToListAsync();
            var startingIndex = allSlots.FindIndex(x => x.SlotId == startingSlotId);
            if (startingIndex < 0)
                return LaneScheduleResult.Fail("Time slot not found.");
            var candidateSlots = allSlots.Skip(startingIndex).ToList();

            var lanes = await _context.Lanes
                .Where(x => x.BranchId == branchId && x.IsActive && x.IsBusinessLane)
                .OrderBy(x => x.LaneId)
                .ToListAsync();
            if (!lanes.Any())
                return LaneScheduleResult.Fail("No available business lane in this branch.");

            var dayStart = targetDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var firstSlotStart = dayStart.Add(candidateSlots[0].StartTime);
            var projectedFreeTimes = await GetLaneProjectedFreeTimesAsync(
                branchId, firstSlotStart, isBusinessLane: true);
            var minimumLaneStart = lanes.ToDictionary(
                lane => lane.LaneId,
                lane => projectedFreeTimes.TryGetValue(lane.LaneId, out var freeAt)
                    ? freeAt
                    : firstSlotStart);
            var reservations = lanes.ToDictionary(
                lane => lane.LaneId,
                _ => new List<LaneReservation>());

            var existingBookings = await _context.Bookings
                .Include(x => x.BookingDetails)
                .Include(x => x.FleetVehicle)
                .Where(x =>
                    x.BranchId == branchId &&
                    x.BookingType == "Business" &&
                    x.BookingId != excludedBookingId &&
                    (x.Status == "Pending" || x.Status == "Confirmed") &&
                    x.ScheduledTime >= dayStart &&
                    x.ScheduledTime < dayEnd)
                .OrderBy(x => x.ScheduledTime)
                .ThenBy(x => x.BookingId)
                .ToListAsync();
            foreach (var booking in existingBookings)
            {
                if (booking.FleetVehicle == null || booking.BookingDetails.Count == 0)
                    continue;
                var serviceIds = booking.BookingDetails.Select(x => x.ServiceId).ToList();
                var prices = await _context.ServicePrices
                    .Where(x =>
                        x.BranchId == branchId &&
                        x.VehicleTypeId == booking.FleetVehicle.VehicleTypeId &&
                        serviceIds.Contains(x.ServiceId))
                    .ToListAsync();
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

            var bookedWeights = await _context.DailySlotCapacities
                .Where(x => x.BranchId == branchId && x.Date == dayStart)
                .ToDictionaryAsync(x => x.SlotId, x => x.BookedWeight);
            if (excludedBookingId.HasValue)
            {
                var excludedBooking = await _context.Bookings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BookingId == excludedBookingId.Value);
                if (excludedBooking != null && excludedBooking.ScheduledTime.Date == dayStart)
                {
                    var excludedSlot = allSlots.FirstOrDefault(x =>
                        x.StartTime <= excludedBooking.ScheduledTime.TimeOfDay &&
                        x.EndTime > excludedBooking.ScheduledTime.TimeOfDay);
                    if (excludedSlot != null && bookedWeights.ContainsKey(excludedSlot.SlotId))
                    {
                        bookedWeights[excludedSlot.SlotId] = Math.Max(
                            0,
                            bookedWeights[excludedSlot.SlotId] - excludedBooking.CapacityWeight);
                    }
                }
            }

            var assignments = new List<VehicleAssignment>();
            foreach (var vehicle in vehicles)
            {
                var duration = WashTimeEstimator.EstimateMinutes(vehicle.ServicePrices);
                VehicleAssignment? assignment = null;
                foreach (var slot in candidateSlots)
                {
                    var bookedWeight = bookedWeights.TryGetValue(slot.SlotId, out var weight)
                        ? weight
                        : 0;
                    if (bookedWeight + vehicle.CapacityWeight > slot.MaxCapacity)
                        continue;

                    var slotStart = dayStart.Add(slot.StartTime);
                    var slotEnd = dayStart.Add(slot.EndTime);
                    var laneChoice = lanes
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
                    bookedWeights[slot.SlotId] = bookedWeight + vehicle.CapacityWeight;
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
