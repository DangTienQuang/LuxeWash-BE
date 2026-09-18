using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class IncidentCapacityService : IIncidentCapacityService
    {
        private readonly AutoWashDbContext _context;

        public IncidentCapacityService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<EffectiveSlotCapacityResult> GetEffectiveSlotCapacityAsync(int branchId, DateTime date, int slotId, BookingContextDTO bookingContext, DateTime nowVn, AutoWashPro.DAL.Entities.BranchIncident? simulatedIncident = null, long? ignoreIncidentId = null)
        {
            var slot = await _context.TimeSlots
                .FirstOrDefaultAsync(s => s.SlotId == slotId && s.BranchId == branchId);

            if (slot == null)
            {
                return new EffectiveSlotCapacityResult { ClosedReason = "INVALID_SLOT" };
            }

            var slotStart = date.Date.Add(slot.StartTime);
            var slotEnd = date.Date.Add(slot.EndTime);

            // Handle cross-midnight slots if EndTime <= StartTime
            if (slot.EndTime <= slot.StartTime)
            {
                slotEnd = slotEnd.AddDays(1);
            }

            var activeIncidentsQuery = _context.BranchIncidents
                .Include(i => i.IncidentLanes)
                .Where(i => i.BranchId == branchId && i.Status == "Active")
                // Intersection check
                .Where(i => i.StartedAtVn < slotEnd && i.EstimatedEndAtVn > slotStart);

            if (ignoreIncidentId.HasValue)
            {
                activeIncidentsQuery = activeIncidentsQuery.Where(i => i.Id != ignoreIncidentId.Value);
            }

            var activeIncidents = await activeIncidentsQuery.ToListAsync();

            if (simulatedIncident != null)
            {
                // Check if simulated incident intersects with this slot
                if (simulatedIncident.StartedAtVn < slotEnd && simulatedIncident.EstimatedEndAtVn > slotStart)
                {
                    activeIncidents.Add(simulatedIncident);
                }
            }

            var dailyCapacity = await _context.DailySlotCapacities
                .FirstOrDefaultAsync(c => c.BranchId == branchId && c.SlotId == slotId && c.Date == date.Date);

            int bookedWeight = dailyCapacity?.BookedWeight ?? 0;

            var result = new EffectiveSlotCapacityResult
            {
                BaseCapacity = slot.MaxCapacity,
                BookedWeight = bookedWeight,
                CapacityVersion = activeIncidents.Count > 0 ? activeIncidents.Sum(i => i.Version) : 0
            };

            var allLanes = await _context.Lanes
                .Where(l => l.BranchId == branchId && l.IsActive)
                .ToListAsync();

            var laneCapacities = await _context.LaneSlotCapacities
                .Where(c => c.SlotId == slotId)
                .ToListAsync();

            if (activeIncidents.Any(i => i.Scope == "WholeBranch"))
            {
                result.EffectiveCapacity = 0;
                result.ClosedReason = "INCIDENT_WHOLE_BRANCH";
                result.AvailableWeight = 0;
                result.IncidentIds = activeIncidents.Select(i => i.Id).ToList();
                return result;
            }

            var affectedLaneIds = activeIncidents
                .SelectMany(i => i.IncidentLanes)
                .Select(il => il.LaneId)
                .Distinct()
                .ToHashSet();

            result.IncidentIds = activeIncidents.Select(i => i.Id).ToList();

            var eligibleLanes = allLanes.Where(l => !affectedLaneIds.Contains(l.LaneId)).ToList();
            
            // Check booking context rules
            if (bookingContext.IsBusiness)
            {
                eligibleLanes = eligibleLanes.Where(l => l.IsBusinessLane).ToList();
            }
            else
            {
                // Looking at old logic, maybe business lanes are strictly for business?
                eligibleLanes = eligibleLanes.Where(l => !l.IsBusinessLane).ToList();
            }

            if (slot.IsVipOnly && !bookingContext.IsVipEligible)
            {
                result.EffectiveCapacity = 0;
                result.ClosedReason = "VIP_ONLY";
                return result;
            }

            result.EligibleLaneIds = eligibleLanes.Select(l => l.LaneId).ToList();

            if (result.EligibleLaneIds.Count == 0)
            {
                result.EffectiveCapacity = 0;
                result.ClosedReason = "NO_ELIGIBLE_LANES";
                result.AvailableWeight = 0;
                return result;
            }

            if (activeIncidents.Count > 0)
            {
                // Calculate effective capacity based on remaining lanes
                // First, check if configuration exists
                if (laneCapacities.Count == 0 || laneCapacities.Any(lc => !lc.IsCalibrated))
                {
                    // Fail closed if uncalibrated
                    result.EffectiveCapacity = 0;
                    result.ClosedReason = "CONFIGURATION_REQUIRED";
                    result.AvailableWeight = 0;
                    return result;
                }

                // Sum of max weight of ALL remaining un-affected lanes (not just eligible for this booking, but the total capacity of the branch for this slot)
                var remainingLanesAll = allLanes.Where(l => !affectedLaneIds.Contains(l.LaneId)).Select(l => l.LaneId).ToList();
                int effectiveTotalCapacity = laneCapacities
                    .Where(lc => remainingLanesAll.Contains(lc.LaneId))
                    .Sum(lc => lc.MaxWeightUnits);

                result.EffectiveCapacity = Math.Min(slot.MaxCapacity, effectiveTotalCapacity);
                result.ClosedReason = "INCIDENT_PARTIAL";
            }
            else
            {
                result.EffectiveCapacity = slot.MaxCapacity;
            }

            result.AvailableWeight = Math.Max(0, result.EffectiveCapacity - bookedWeight);
            
            // Reconcile ETA overdue
            if (activeIncidents.Any(i => nowVn >= i.EstimatedEndAtVn))
            {
                result.EffectiveCapacity = 0;
                result.AvailableWeight = 0;
                result.ClosedReason = "INCIDENT_OVERDUE";
            }

            return result;
        }
    }
}
