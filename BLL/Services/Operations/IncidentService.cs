using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using BLL.Helpers;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class IncidentService : IIncidentService
    {
        private readonly AutoWashDbContext _context;
        private readonly IIncidentCapacityService _capacityService;
        private readonly IIncidentCustomerService _customerService;

        public IncidentService(AutoWashDbContext context, IIncidentCapacityService capacityService, IIncidentCustomerService customerService)
        {
            _context = context;
            _capacityService = capacityService;
            _customerService = customerService;
        }

        private async Task ValidateManagerBranchAsync(int managerUserId, int branchId)
        {
            var employee = await _context.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == managerUserId);
            if (employee == null || employee.BranchId != branchId)
                throw new UnauthorizedException("User is not authorized for this branch.");
        }

        public async Task<IncidentPagedResponseDTO> GetIncidentsAsync(int managerUserId, int page = 1, int pageSize = 10)
        {
            var employee = await _context.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == managerUserId);
            if (employee == null || !employee.BranchId.HasValue)
                throw new UnauthorizedException("User is not authorized for any branch.");

            int branchId = employee.BranchId.Value;

            var query = _context.BranchIncidents
                .Include(i => i.IncidentLanes)
                .Where(i => i.BranchId == branchId);

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(i => i.CreatedAtVn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new BranchIncidentDTO
                {
                    IncidentId = i.Id,
                    BranchId = i.BranchId,
                    BranchName = i.Branch.Name,
                    Type = i.Type,
                    Scope = i.Scope,
                    Reason = i.Reason,
                    Status = i.Status,
                    EstimatedEndAtVn = i.EstimatedEndAtVn,
                    CreatedAtVn = i.CreatedAtVn,
                    ResolvedAtVn = i.ActualEndAtVn,
                    CreatedByUserId = i.CreatedByUserId,
                    LaneIds = i.IncidentLanes.Select(l => l.LaneId).ToList()
                })
                .ToListAsync();

            return new IncidentPagedResponseDTO
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<BranchIncidentDTO> GetIncidentAsync(int managerUserId, long incidentId)
        {
            var employee = await _context.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == managerUserId);
            if (employee == null || !employee.BranchId.HasValue)
                throw new UnauthorizedException("User is not authorized for any branch.");

            int branchId = employee.BranchId.Value;

            var incident = await _context.BranchIncidents
                .Include(i => i.Branch)
                .Include(i => i.IncidentLanes)
                .FirstOrDefaultAsync(i => i.Id == incidentId && i.BranchId == branchId);

            if (incident == null)
                throw new NotFoundException("Incident not found.");

            return new BranchIncidentDTO
            {
                IncidentId = incident.Id,
                BranchId = incident.BranchId,
                BranchName = incident.Branch.Name,
                Type = incident.Type,
                Scope = incident.Scope,
                Reason = incident.Reason,
                Status = incident.Status,
                EstimatedEndAtVn = incident.EstimatedEndAtVn,
                CreatedAtVn = incident.CreatedAtVn,
                ResolvedAtVn = incident.ActualEndAtVn,
                CreatedByUserId = incident.CreatedByUserId,
                LaneIds = incident.IncidentLanes.Select(l => l.LaneId).ToList()
            };
        }

        public async Task<List<IncidentAffectedBookingDTO>> GetIncidentImpactAsync(int managerUserId, long incidentId)
        {
            var incident = await GetIncidentAsync(managerUserId, incidentId);

            var impactedEntities = await _context.IncidentAffectedBookings
                .Include(b => b.Booking)
                .Where(b => b.IncidentId == incidentId)
                .AsNoTracking()
                .ToListAsync();

            var targetBranchIds = impactedEntities
                .Where(item => item.TargetBranchId.HasValue)
                .Select(item => item.TargetBranchId!.Value)
                .Distinct()
                .ToList();
            var targetSlotIds = impactedEntities
                .Where(item => item.TargetSlotId.HasValue)
                .Select(item => item.TargetSlotId!.Value)
                .Distinct()
                .ToList();

            var branchNames = await _context.Branches
                .Where(branch => targetBranchIds.Contains(branch.BranchId))
                .ToDictionaryAsync(branch => branch.BranchId, branch => branch.Name);
            var slots = await _context.TimeSlots
                .Where(slot => targetSlotIds.Contains(slot.SlotId))
                .ToDictionaryAsync(slot => slot.SlotId);

            return impactedEntities.Select(item =>
            {
                string? targetBranchName = null;
                TimeSlot? targetSlot = null;
                var hasTargetBranch = item.TargetBranchId.HasValue &&
                    branchNames.TryGetValue(item.TargetBranchId.Value, out targetBranchName);
                var hasTargetSlot = item.TargetSlotId.HasValue &&
                    slots.TryGetValue(item.TargetSlotId.Value, out targetSlot);

                return new IncidentAffectedBookingDTO
                {
                    AffectedBookingId = item.Id,
                    BookingId = item.BookingId,
                    // Booking.LicensePlate is the canonical snapshot for both personal and
                    // business bookings. Fleet bookings do not have Booking.Vehicle.
                    LicensePlate = item.Booking.LicensePlate,
                    BookingType = item.Booking.BusinessProfileId.HasValue ? "Business" : "Personal",
                    // Keep the impact table anchored to the appointment that was disrupted.
                    // Booking.ScheduledTime changes after a successful transfer.
                    ScheduledTime = item.OriginalScheduledTimeVn.ToString("yyyy-MM-dd HH:mm"),
                    CustomerAction = item.Status,
                    SystemResolution = item.Decision ?? "",
                    CustomerDeadlineVn = item.ResponseDeadlineAtVn,
                    AlternativeBranchId = item.TargetBranchId?.ToString(),
                    AlternativeBranchName = hasTargetBranch ? targetBranchName : null,
                    AlternativeTimeSlot = item.TargetSlotId?.ToString(),
                    AlternativeTimeSlotLabel = hasTargetSlot
                        ? $"{targetSlot!.StartTime:hh\\:mm} - {targetSlot.EndTime:hh\\:mm}"
                        : null
                };
            }).ToList();
        }

        public async Task<PreviewIncidentResponseDTO> PreviewIncidentImpactAsync(int managerUserId, PreviewIncidentRequestDTO request)
        {
            await ValidateManagerBranchAsync(managerUserId, request.BranchId);

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (request.EstimatedEndAtVn <= now)
                throw new BadRequestException("EstimatedEndAtVn must be a future Vietnam local wall-clock time (UTC+7, without Z).");

            if (request.Scope == "SelectedLanes" && (request.LaneIds == null || request.LaneIds.Count == 0))
                throw new BadRequestException("LaneIds must be provided when scope is SelectedLanes.");

            var activeBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.FleetVehicle)
                .Include(b => b.BookingDetails)
                .Where(b => b.BranchId == request.BranchId && b.UserId != null && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime < request.EstimatedEndAtVn && b.ScheduledTime.AddMinutes(60) > now) // Approximation
                .ToListAsync();

            var slots = await _context.TimeSlots.Where(s => s.BranchId == request.BranchId).ToListAsync();

            var response = new PreviewIncidentResponseDTO();
            int totalCapacityLoss = 0;
            
            var simulatedIncident = new BranchIncident
            {
                BranchId = request.BranchId,
                Type = request.Type,
                Scope = request.Scope,
                StartedAtVn = now,
                EstimatedEndAtVn = request.EstimatedEndAtVn,
                Status = "Active",
                Version = 1,
                IncidentLanes = request.LaneIds?.Select(id => new IncidentLane { LaneId = id }).ToList() ?? new List<IncidentLane>()
            };

            var capacityContext = new BookingContextDTO
            {
                // This calculation measures branch capacity, not whether a new
                // customer may enter a VIP-only time slot. Existing bookings in
                // the slot have already passed that eligibility check.
                IsVipEligible = true,
                CapacityWeight = 1
            };

            var capacityBySlotOccurrence = new Dictionary<(DateTime Date, int SlotId), EffectiveSlotCapacityResult>();
            for (var date = now.Date; date <= request.EstimatedEndAtVn.Date; date = date.AddDays(1))
            {
                foreach (var slot in slots)
                {
                    var slotStart = date.Add(slot.StartTime);
                    var slotEnd = date.Add(slot.EndTime);
                    if (slot.EndTime <= slot.StartTime)
                    {
                        slotEnd = slotEnd.AddDays(1);
                    }

                    if (slotStart >= request.EstimatedEndAtVn || slotEnd <= now)
                    {
                        continue;
                    }

                    var capacity = await _capacityService.GetEffectiveSlotCapacityAsync(
                        request.BranchId,
                        date,
                        slot.SlotId,
                        capacityContext,
                        now,
                        simulatedIncident);
                    capacityBySlotOccurrence[(date, slot.SlotId)] = capacity;
                    totalCapacityLoss += Math.Max(0, capacity.BaseCapacity - capacity.EffectiveCapacity);
                }
            }

            var bookingsBySlot = activeBookings.GroupBy(b => new
            {
                Date = b.ScheduledTime.Date,
                SlotId = slots.FirstOrDefault(s =>
                    s.StartTime <= b.ScheduledTime.TimeOfDay &&
                    (s.EndTime > s.StartTime
                        ? s.EndTime > b.ScheduledTime.TimeOfDay
                        : b.ScheduledTime.TimeOfDay >= s.StartTime || b.ScheduledTime.TimeOfDay < s.EndTime))?.SlotId ?? 0
            });

            foreach (var group in bookingsBySlot)
            {
                int slotId = group.Key.SlotId;
                if (slotId == 0) continue;

                var slotBookings = group.OrderByDescending(b => b.BookingId).ToList(); // LIFO
                if (!capacityBySlotOccurrence.TryGetValue((group.Key.Date, slotId), out var cap))
                {
                    cap = await _capacityService.GetEffectiveSlotCapacityAsync(
                        request.BranchId,
                        group.Key.Date,
                        slotId,
                        capacityContext,
                        now,
                        simulatedIncident);
                }
                
                int effectiveCapacity = cap.EffectiveCapacity;
                int overbookedAmount = cap.BookedWeight - effectiveCapacity;

                foreach (var b in slotBookings)
                {
                    int weight = b.CapacityWeight > 0 ? b.CapacityWeight : 1;
                    
                    bool isOverbooked = false;
                    if (overbookedAmount > 0)
                    {
                        isOverbooked = true;
                        overbookedAmount -= weight;
                    }

                    response.AffectedBookings.Add(new AffectedBookingSummaryDTO
                    {
                        BookingId = b.BookingId,
                        LicensePlate = b.LicensePlate,
                        BookingType = b.BusinessProfileId.HasValue ? "Business" : "Personal",
                        ScheduledTime = b.ScheduledTime.ToString("yyyy-MM-dd HH:mm"),
                        CapacityWeight = weight,
                        IsOverbooked = isOverbooked
                    });
                }
            }
            
            response.AffectedBookingsCount = response.AffectedBookings.Count;

            response.TotalCapacityLoss = totalCapacityLoss;

            return response;
        }

        public async Task<long> CreateIncidentAsync(int managerUserId, CreateIncidentRequestDTO request)
        {
            await ValidateManagerBranchAsync(managerUserId, request.BranchId);

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (request.EstimatedEndAtVn <= now)
                throw new BadRequestException("EstimatedEndAtVn must be a future Vietnam local wall-clock time (UTC+7, without Z).");

            if (request.Scope == "SelectedLanes" && (request.LaneIds == null || request.LaneIds.Count == 0))
                throw new BadRequestException("LaneIds must be provided when scope is SelectedLanes.");

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);


            var sysVoucherCode = "INCIDENT_COMP_20";
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == sysVoucherCode);
            if (voucher == null)
            {
                voucher = new Voucher
                {
                    Code = sysVoucherCode,
                    DiscountAmount = 0,
                    DiscountPercent = 20,
                    IsActive = true,
                    StartDate = now.Date,
                    ExpiryDate = now.AddYears(10)
                };
                _context.Vouchers.Add(voucher);

                await _context.SaveChangesAsync();
            }

            var incident = new BranchIncident
            {
                BranchId = request.BranchId,
                Type = request.Type,
                Scope = request.Scope,
                Reason = request.Reason,
                StartedAtVn = now,
                EstimatedEndAtVn = request.EstimatedEndAtVn,
                Status = "Active",
                CreatedByUserId = managerUserId,
                CreatedAtVn = now,
                UpdatedAtVn = now,
                Version = 1
            };

            _context.BranchIncidents.Add(incident);
            await _context.SaveChangesAsync(); // get ID

            if (request.Scope == "SelectedLanes" && request.LaneIds != null)
            {

                var validLanes = await _context.Lanes.Where(l => l.BranchId == request.BranchId && request.LaneIds.Contains(l.LaneId)).Select(l => l.LaneId).ToListAsync();
                if (validLanes.Count != request.LaneIds.Count)
                {
                    throw new BadRequestException("One or more LaneIds do not belong to the specified branch.");
                }

                foreach (var laneId in request.LaneIds)
                {
                    _context.IncidentLanes.Add(new IncidentLane
                    {
                        IncidentId = incident.Id,
                        LaneId = laneId
                    });
                }
            }

            _context.IncidentChanges.Add(new IncidentChange
            {
                IncidentId = incident.Id,
                ActorUserId = managerUserId,
                Action = "Created",
                NewEstimatedEndAtVn = request.EstimatedEndAtVn,
                OccurredAtVn = now
            });


            var activeBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.FleetVehicle)
                .Include(b => b.BookingDetails)
                .Where(b => b.BranchId == request.BranchId && b.UserId != null && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime < request.EstimatedEndAtVn && b.ScheduledTime.AddMinutes(60) > now)
                .ToListAsync();

            var deadline = now.AddMinutes(30);
            
            var affectedBookingIds = request.SelectedBookingIds != null ? new HashSet<int>(request.SelectedBookingIds) : new HashSet<int>();

            // Basic safety check: Only allow affecting bookings that actually overlap the timeframe for this branch
            var validActiveBookingIds = activeBookings.Select(b => b.BookingId).ToHashSet();
            affectedBookingIds.IntersectWith(validActiveBookingIds);

            foreach (var b in activeBookings)
            {
                if (affectedBookingIds.Contains(b.BookingId))
                {
                    var affectedBooking = new IncidentAffectedBooking
                    {
                        IncidentId = incident.Id,
                        BookingId = b.BookingId,
                        ActiveBookingId = b.BookingId,
                        UserId = b.UserId,
                        Status = "AwaitingCustomer",
                        ResponseDeadlineAtVn = deadline,
                        OriginalBranchId = request.BranchId,
                        OriginalScheduledTimeVn = b.ScheduledTime
                    };
                    _context.IncidentAffectedBookings.Add(affectedBooking);


                    var msg = new OutboxMessage
                    {
                        Type = "INCIDENT_ACTION_REQUIRED",
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = b.BookingId, IncidentId = incident.Id }),
                        CreatedAt = now,
                        NextRetryAt = now
                    };
                    _context.OutboxMessages.Add(msg);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return incident.Id;
        }

        public async Task ExtendIncidentAsync(int managerUserId, long incidentId, ExtendIncidentRequestDTO request)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            
            var employee = await _context.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == managerUserId);
            if (employee == null || !employee.BranchId.HasValue) throw new UnauthorizedException("User is not authorized for any branch.");
            int branchId = employee.BranchId.Value;

            var incident = await _context.BranchIncidents
                .Include(i => i.IncidentLanes)
                .FirstOrDefaultAsync(i => i.Id == incidentId && i.Status == "Active" && i.BranchId == branchId);
            if (incident == null) throw new NotFoundException("Active incident not found in your branch.");
            if (request.NewEstimatedEndAtVn <= now || request.NewEstimatedEndAtVn <= incident.EstimatedEndAtVn)
                throw new BadRequestException("NewEstimatedEndAtVn must be later than the current ETA and use Vietnam local wall-clock time (UTC+7, without Z).");

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            _context.IncidentChanges.Add(new IncidentChange
            {
                IncidentId = incident.Id,
                ActorUserId = managerUserId,
                Action = "Extended",
                OldEstimatedEndAtVn = incident.EstimatedEndAtVn,
                NewEstimatedEndAtVn = request.NewEstimatedEndAtVn,
                OccurredAtVn = now,
                Note = request.Note
            });

            var oldEnd = incident.EstimatedEndAtVn;


            incident.EstimatedEndAtVn = request.NewEstimatedEndAtVn;
            incident.UpdatedAtVn = now;
            incident.Version++;


            var newBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.FleetVehicle)
                .Include(b => b.BookingDetails)
                .Where(b => b.BranchId == incident.BranchId && b.UserId != null && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime >= oldEnd && b.ScheduledTime < request.NewEstimatedEndAtVn)
                .ToListAsync();

            var deadline = now.AddMinutes(30);
            var affectedBookingIds = request.SelectedBookingIds != null ? new HashSet<int>(request.SelectedBookingIds) : new HashSet<int>();
            var validNewBookingIds = newBookings.Select(b => b.BookingId).ToHashSet();
            affectedBookingIds.IntersectWith(validNewBookingIds);

            foreach (var b in newBookings)
            {
                if (affectedBookingIds.Contains(b.BookingId))
                {
                    if (!await _context.IncidentAffectedBookings.AnyAsync(c => c.BookingId == b.BookingId && c.IncidentId == incident.Id))
                    {
                        var affectedBooking = new IncidentAffectedBooking
                        {
                            IncidentId = incident.Id,
                            BookingId = b.BookingId,
                            ActiveBookingId = b.BookingId,
                            UserId = b.UserId,
                            Status = "AwaitingCustomer",
                            ResponseDeadlineAtVn = deadline,
                            OriginalBranchId = incident.BranchId,
                            OriginalScheduledTimeVn = b.ScheduledTime
                        };
                        _context.IncidentAffectedBookings.Add(affectedBooking);

                        var msg = new OutboxMessage
                        {
                            Type = "INCIDENT_ACTION_REQUIRED",
                            Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = b.BookingId, IncidentId = incident.Id }),
                            CreatedAt = now,
                            NextRetryAt = now
                        };
                        _context.OutboxMessages.Add(msg);
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task ResolveIncidentAsync(int managerUserId, long incidentId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;

            var employee = await _context.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == managerUserId);
            if (employee == null || !employee.BranchId.HasValue) throw new UnauthorizedException("User is not authorized for any branch.");
            int branchId = employee.BranchId.Value;

            var incident = await _context.BranchIncidents.FirstOrDefaultAsync(i => i.Id == incidentId && i.Status == "Active" && i.BranchId == branchId);
            if (incident == null) throw new NotFoundException("Active incident not found in your branch.");

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            incident.Status = "Resolved";
            incident.ActualEndAtVn = now;
            incident.ResolvedByUserId = managerUserId;
            incident.UpdatedAtVn = now;
            incident.Version++;

            _context.IncidentChanges.Add(new IncidentChange
            {
                IncidentId = incident.Id,
                ActorUserId = managerUserId,
                Action = "Resolved",
                OccurredAtVn = now
            });




            var pendingCases = await _context.IncidentAffectedBookings
                .Include(b => b.Booking)
                .ThenInclude(b => b.Vehicle)
                .Include(b => b.Booking)
                .ThenInclude(b => b.FleetVehicle)
                .Include(b => b.Booking)
                .ThenInclude(b => b.BookingDetails)
                .Include(b => b.Booking)
                .ThenInclude(b => b.User)
                .ThenInclude(u => u!.CustomerProfile)
                .ThenInclude(p => p.Tier)
                .Where(b => b.IncidentId == incident.Id && b.Status == "AwaitingCustomer")
                .ToListAsync();

            var slots = await _context.TimeSlots.Where(s => s.BranchId == incident.BranchId).ToListAsync();

            var casesToCancel = new List<AutoWashPro.DAL.Entities.IncidentAffectedBooking>();
            var slotBookedWeights = new Dictionary<(int SlotId, DateTime Date, bool IsBusiness), int>();

            foreach (var c in pendingCases)
            {
                var b = c.Booking;
                var ctx = new BookingContextDTO
                {
                    IsBusiness = b.BusinessProfileId.HasValue,
                    IsVipEligible = CustomerEligibilityHelper.IsVipEligible(b.User?.CustomerProfile),
                    VehicleTypeId = b.ActualVehicleTypeId ??
                        b.FleetVehicle?.VehicleTypeId ??
                        b.Vehicle?.VehicleTypeId,
                    ServiceIds = b.BookingDetails.Select(d => d.ServiceId).ToList(),
                    CapacityWeight = b.CapacityWeight > 0 ? b.CapacityWeight : 1
                };
                
                var slot = slots.FirstOrDefault(s =>
                    s.StartTime <= b.ScheduledTime.TimeOfDay &&
                    s.EndTime > b.ScheduledTime.TimeOfDay);
                int slotId = slot?.SlotId ?? 0;
                var capacityKey = (slotId, b.ScheduledTime.Date, b.BusinessProfileId.HasValue);
                
                var cap = await _capacityService.GetEffectiveSlotCapacityAsync(incident.BranchId, b.ScheduledTime.Date, slotId, ctx, now, ignoreIncidentId: incident.Id);
                
                if (!slotBookedWeights.ContainsKey(capacityKey))
                {
                    slotBookedWeights[capacityKey] = cap.BookedWeight;
                }

                if (cap.EffectiveCapacity >= slotBookedWeights[capacityKey])
                {
                    c.Status = "Kept";
                    c.Decision = "Keep";
                    c.DecidedAtVn = now;
                    c.ActiveBookingId = null;
                    
                    _context.OutboxMessages.Add(new OutboxMessage
                    {
                        Type = "INCIDENT_RESOLVED_EARLY",
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = c.BookingId, IncidentId = incident.Id }),
                        CreatedAt = now,
                        NextRetryAt = now
                    });
                }
                else
                {
                    casesToCancel.Add(c);
                    slotBookedWeights[capacityKey] = Math.Max(0, slotBookedWeights[capacityKey] - ctx.CapacityWeight);

                    _context.OutboxMessages.Add(new OutboxMessage
                    {
                        Type = "INCIDENT_SYSTEM_CANCELLED",
                        Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = c.BookingId, IncidentId = incident.Id, Note = "Cancelled due to overcapacity after resolution" }),
                        CreatedAt = now,
                        NextRetryAt = now
                    });
                }
            }

            foreach (var c in casesToCancel)
            {
                await _customerService.SystemCancelAsync(c.UserId ?? 0, c.BookingId, c.Id);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }
}
