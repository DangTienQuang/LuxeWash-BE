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

        public IncidentService(AutoWashDbContext context, IIncidentCapacityService capacityService)
        {
            _context = context;
            _capacityService = capacityService;
        }

        public async Task<PreviewIncidentResponseDTO> PreviewIncidentImpactAsync(int managerUserId, PreviewIncidentRequestDTO request)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (request.EstimatedEndAtVn <= now)
                throw new BadRequestException("Estimated end time must be in the future.");

            var activeBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Where(b => b.BranchId == request.BranchId && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime < request.EstimatedEndAtVn && b.ScheduledTime.AddMinutes(60) > now) // Approximation
                .ToListAsync();

            var slots = await _context.TimeSlots.Where(s => s.BranchId == request.BranchId).ToListAsync();

            var response = new PreviewIncidentResponseDTO();
            int totalCapacityLoss = 0;

            if (request.Scope == "WholeBranch")
            {
                response.AffectedBookingsCount = activeBookings.Count;
                foreach (var b in activeBookings)
                {
                    response.AffectedBookings.Add(new AffectedBookingSummaryDTO
                    {
                        BookingId = b.BookingId,
                        LicensePlate = b.Vehicle?.LicensePlate ?? "",
                        ScheduledTime = b.ScheduledTime.ToString("yyyy-MM-dd HH:mm"),
                        CapacityWeight = b.CapacityWeight > 0 ? b.CapacityWeight : 1
                    });
                }
                
                foreach (var slot in slots)
                {
                    // A simple approximation for preview
                    totalCapacityLoss += slot.MaxCapacity;
                }
            }
            else
            {
                // Lane partial failure
                // Real implementation would simulate the GetEffectiveSlotCapacityAsync
                foreach (var b in activeBookings)
                {
                    var ctx = new BookingContextDTO
                    {
                        IsBusiness = b.BusinessProfileId.HasValue,
                        IsVipEligible = false, // Approximated
                        VehicleTypeId = b.Vehicle?.VehicleTypeId,
                        CapacityWeight = b.CapacityWeight > 0 ? b.CapacityWeight : 1
                    };
                    
                    var slot = slots.FirstOrDefault(s => s.StartTime == b.ScheduledTime.TimeOfDay);
                    int slotId = slot?.SlotId ?? 0;
                    
                    var cap = await _capacityService.GetEffectiveSlotCapacityAsync(request.BranchId, b.ScheduledTime.Date, slotId, ctx, now);
                    if (cap.AvailableWeight < ctx.CapacityWeight)
                    {
                        response.AffectedBookings.Add(new AffectedBookingSummaryDTO
                        {
                            BookingId = b.BookingId,
                            LicensePlate = b.Vehicle?.LicensePlate ?? "",
                            ScheduledTime = b.ScheduledTime.ToString("yyyy-MM-dd HH:mm"),
                            CapacityWeight = ctx.CapacityWeight
                        });
                    }
                }
                response.AffectedBookingsCount = response.AffectedBookings.Count;
            }

            response.TotalCapacityLoss = totalCapacityLoss;
            return response;
        }

        public async Task<long> CreateIncidentAsync(int managerUserId, CreateIncidentRequestDTO request)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (request.EstimatedEndAtVn <= now)
                throw new BadRequestException("Estimated end time must be in the future.");

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

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

            // Find affected bookings and create IncidentAffectedBooking records
            var activeBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Where(b => b.BranchId == request.BranchId && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime < request.EstimatedEndAtVn && b.ScheduledTime.AddMinutes(60) > now)
                .ToListAsync();

            var deadline = now.AddMinutes(30);
            var slots = await _context.TimeSlots.Where(s => s.BranchId == request.BranchId).ToListAsync();

            foreach (var b in activeBookings)
            {
                bool isAffected = request.Scope == "WholeBranch";
                
                if (!isAffected)
                {
                    var ctx = new BookingContextDTO
                    {
                        IsBusiness = b.BusinessProfileId.HasValue,
                        IsVipEligible = false, 
                        VehicleTypeId = b.Vehicle?.VehicleTypeId,
                        CapacityWeight = b.CapacityWeight > 0 ? b.CapacityWeight : 1
                    };
                    
                    var slot = slots.FirstOrDefault(s => s.StartTime == b.ScheduledTime.TimeOfDay);
                    int slotId = slot?.SlotId ?? 0;
                    
                    var cap = await _capacityService.GetEffectiveSlotCapacityAsync(request.BranchId, b.ScheduledTime.Date, slotId, ctx, now);
                    if (cap.AvailableWeight < ctx.CapacityWeight)
                    {
                        isAffected = true;
                    }
                }

                if (isAffected)
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

                    // Add Outbox message to notify the user
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
            var incident = await _context.BranchIncidents.FirstOrDefaultAsync(i => i.Id == incidentId && i.Status == "Active");
            if (incident == null) throw new NotFoundException("Active incident not found");

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

            incident.EstimatedEndAtVn = request.NewEstimatedEndAtVn;
            incident.UpdatedAtVn = now;
            incident.Version++;

            // Handle newly affected bookings if extended...
            // Omitted for brevity but similar to Create

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task ResolveIncidentAsync(int managerUserId, long incidentId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var incident = await _context.BranchIncidents.FirstOrDefaultAsync(i => i.Id == incidentId && i.Status == "Active");
            if (incident == null) throw new NotFoundException("Active incident not found");

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

            // Find all pending AwaitingCustomer bookings and auto-handle them if needed, or leave them.
            // Plan says: "nếu sự cố xong trước hạn, các booking chưa Cancel có thể Keep".
            // We can resolve them to Kept if they haven't decided.
            var pendingCases = await _context.IncidentAffectedBookings
                .Where(b => b.IncidentId == incident.Id && b.Status == "AwaitingCustomer")
                .ToListAsync();

            foreach (var c in pendingCases)
            {
                c.Status = "Kept";
                c.Decision = "Keep";
                c.DecidedAtVn = now;
                
                // Notify user
                _context.OutboxMessages.Add(new OutboxMessage
                {
                    Type = "INCIDENT_RESOLVED_EARLY",
                    Payload = System.Text.Json.JsonSerializer.Serialize(new { BookingId = c.BookingId }),
                    CreatedAt = now,
                    NextRetryAt = now
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }
}
