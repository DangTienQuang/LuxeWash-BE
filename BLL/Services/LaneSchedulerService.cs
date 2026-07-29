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
        private const int SlotGraceMinutes = 15;

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
                    // Lane is free physically right now
                    result[lane.LaneId] = slotStart;
                }
                else
                {
                    // Lane is currently occupied. We estimate it will be free in 15 minutes from when it was occupied.
                    // (This is only used for Fleet Simulation in advance, NOT for realtime barrier/queue logic)
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

            var projectedFreeTimes = await GetLaneProjectedFreeTimesAsync(branchId, slotStart);

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

                int washMinutes = WashTimeEstimator.EstimateMinutes(servicePrices);
                DateTime estimatedStart = laneState.FreeAt < slotStart ? slotStart : laneState.FreeAt;
                DateTime estimatedEnd = estimatedStart.AddMinutes(washMinutes);

                laneState.FreeAt = estimatedEnd.AddMinutes(WashTimeEstimator.GetInterVehicleBuffer());
            }

            var assignments = new List<VehicleAssignment>();
            DateTime deadline = slotStart + slotDuration + TimeSpan.FromMinutes(SlotGraceMinutes);

            foreach (var vehicle in vehicles)
            {
                int washMinutes = WashTimeEstimator.EstimateMinutes(vehicle.ServicePrices);
                laneQueue.Sort((a, b) => a.FreeAt.CompareTo(b.FreeAt));
                var chosenLane = laneQueue[0];

                DateTime estimatedStart = chosenLane.FreeAt < slotStart ? slotStart : chosenLane.FreeAt;
                DateTime estimatedEnd = estimatedStart.AddMinutes(washMinutes);

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

                chosenLane.FreeAt = estimatedEnd.AddMinutes(WashTimeEstimator.GetInterVehicleBuffer());
            }

            return LaneScheduleResult.Ok(assignments);
        }
    }
}