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

            var impacted = await _context.IncidentAffectedBookings
                .Include(b => b.Booking)
                .ThenInclude(b => b.Vehicle)
                .Where(b => b.IncidentId == incidentId)
                .Select(b => new IncidentAffectedBookingDTO
                {
                    AffectedBookingId = b.Id,
                    BookingId = b.BookingId,
                    LicensePlate = b.Booking.Vehicle != null ? b.Booking.Vehicle.LicensePlate : "",
                    ScheduledTime = b.Booking.ScheduledTime.ToString("yyyy-MM-dd HH:mm"),
                    CustomerAction = b.Status, // AwaitingCustomer, Kept, CancelledBySystem, etc.
                    SystemResolution = b.Decision ?? "",
                    CustomerDeadlineVn = b.ResponseDeadlineAtVn,
                    AlternativeBranchId = b.TargetBranchId.ToString(),
                    AlternativeTimeSlot = b.TargetSlotId.ToString()
                })
                .ToListAsync();

            return impacted;
        }

        public async Task<PreviewIncidentResponseDTO> PreviewIncidentImpactAsync(int managerUserId, PreviewIncidentRequestDTO request)
        {
            await ValidateManagerBranchAsync(managerUserId, request.BranchId);

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (request.EstimatedEndAtVn <= now)
                throw new BadRequestException("Estimated end time must be in the future.");

            if (request.Scope == "SelectedLanes" && (request.LaneIds == null || request.LaneIds.Count == 0))
                throw new BadRequestException("LaneIds must be provided when scope is SelectedLanes.");

            var activeBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.BookingDetails)
                .Where(b => b.BranchId == request.BranchId && b.UserId != null && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime < request.EstimatedEndAtVn && b.ScheduledTime.AddMinutes(60) > now) // Approximation
                .ToListAsync();

            var slots = await _context.TimeSlots.Where(s => s.BranchId == request.BranchId).ToListAsync();

            var response = new PreviewIncidentResponseDTO();
            int totalCapacityLoss = 0;

            if (request.Scope == "WholeBranch")
            {
                foreach (var slot in slots)
                {
                    totalCapacityLoss += slot.MaxCapacity;
                }
            }
            
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

            var bookingsBySlot = activeBookings.GroupBy(b => slots.FirstOrDefault(s => s.StartTime == b.ScheduledTime.TimeOfDay)?.SlotId ?? 0);

            foreach (var group in bookingsBySlot)
            {
                int slotId = group.Key;
                if (slotId == 0) continue;

                var slotBookings = group.OrderByDescending(b => b.BookingId).ToList(); // LIFO
                var firstBooking = slotBookings.First();

                var ctx = new BookingContextDTO
                {
                    IsBusiness = firstBooking.BusinessProfileId.HasValue,
                    IsVipEligible = false,
                    VehicleTypeId = firstBooking.Vehicle?.VehicleTypeId,
                    ServiceIds = firstBooking.BookingDetails.Select(d => d.ServiceId).ToList(),
                    CapacityWeight = 1
                };

                var cap = await _capacityService.GetEffectiveSlotCapacityAsync(request.BranchId, firstBooking.ScheduledTime.Date, slotId, ctx, now, simulatedIncident);
                
                int effectiveCapacity = cap.EffectiveCapacity;
                int overbookedAmount = cap.BookedWeight - effectiveCapacity;

                if (overbookedAmount > 0)
                {
                    foreach (var b in slotBookings)
                    {
                        int weight = b.CapacityWeight > 0 ? b.CapacityWeight : 1;
                        response.AffectedBookings.Add(new AffectedBookingSummaryDTO
                        {
                            BookingId = b.BookingId,
                            LicensePlate = b.Vehicle?.LicensePlate ?? "",
                            ScheduledTime = b.ScheduledTime.ToString("yyyy-MM-dd HH:mm"),
                            CapacityWeight = weight
                        });
                        
                        overbookedAmount -= weight;
                        if (overbookedAmount <= 0) break;
                    }
                }
            }
            
            response.AffectedBookingsCount = response.AffectedBookings.Count;

            response.TotalCapacityLoss = totalCapacityLoss;

            var affectedBookingIds = response.AffectedBookings.Select(b => b.BookingId).OrderBy(id => id).ToList();
            var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join(",", affectedBookingIds)));
            response.ExpectedAffectedHash = Convert.ToBase64String(hashBytes);

            return response;
        }

        public async Task<long> CreateIncidentAsync(int managerUserId, CreateIncidentRequestDTO request)
        {
            await ValidateManagerBranchAsync(managerUserId, request.BranchId);

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (request.EstimatedEndAtVn <= now)
                throw new BadRequestException("Estimated end time must be in the future.");

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
                .Include(b => b.BookingDetails)
                .Where(b => b.BranchId == request.BranchId && b.UserId != null && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime < request.EstimatedEndAtVn && b.ScheduledTime.AddMinutes(60) > now)
                .ToListAsync();

            var deadline = now.AddMinutes(30);
            var slots = await _context.TimeSlots.Where(s => s.BranchId == request.BranchId).ToListAsync();

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

            var affectedBookingIds = new HashSet<int>();

            var bookingsBySlot = activeBookings.GroupBy(b => slots.FirstOrDefault(s => s.StartTime == b.ScheduledTime.TimeOfDay)?.SlotId ?? 0);

            foreach (var group in bookingsBySlot)
            {
                int slotId = group.Key;
                if (slotId == 0) continue;

                var slotBookings = group.OrderByDescending(b => b.BookingId).ToList(); // LIFO
                var firstBooking = slotBookings.First();

                if (request.Scope == "WholeBranch")
                {
                    foreach (var b in slotBookings)
                    {
                        affectedBookingIds.Add(b.BookingId);
                    }
                }
                else
                {
                    var ctx = new BookingContextDTO
                    {
                        IsBusiness = firstBooking.BusinessProfileId.HasValue,
                        IsVipEligible = false, 
                        VehicleTypeId = firstBooking.Vehicle?.VehicleTypeId,
                        ServiceIds = firstBooking.BookingDetails.Select(d => d.ServiceId).ToList(),
                        CapacityWeight = 1
                    };

                    var cap = await _capacityService.GetEffectiveSlotCapacityAsync(request.BranchId, firstBooking.ScheduledTime.Date, slotId, ctx, now, simulatedIncident);
                    
                    int effectiveCapacity = cap.EffectiveCapacity;
                    int overbookedAmount = cap.BookedWeight - effectiveCapacity;

                    if (overbookedAmount > 0)
                    {
                        foreach (var b in slotBookings)
                        {
                            affectedBookingIds.Add(b.BookingId);
                            int weight = b.CapacityWeight > 0 ? b.CapacityWeight : 1;
                            overbookedAmount -= weight;
                            if (overbookedAmount <= 0) break;
                        }
                    }
                }
            }

            if (request.ExpectedAffectedCount.HasValue && affectedBookingIds.Count != request.ExpectedAffectedCount.Value)
            {
                throw new ConflictException($"Dữ liệu đã thay đổi (dự kiến {request.ExpectedAffectedCount.Value}, thực tế {affectedBookingIds.Count}), vui lòng tải lại trang hoặc Preview lại sự cố.");
            }

            var currentHashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join(",", affectedBookingIds.OrderBy(id => id))));
            var currentHash = Convert.ToBase64String(currentHashBytes);

            if (!string.IsNullOrEmpty(request.ExpectedAffectedHash) && currentHash != request.ExpectedAffectedHash)
            {
                throw new ConflictException("Dữ liệu đã thay đổi, vui lòng tải lại trang hoặc Preview lại sự cố.");
            }

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

            var incident = await _context.BranchIncidents.FirstOrDefaultAsync(i => i.Id == incidentId && i.Status == "Active" && i.BranchId == branchId);
            if (incident == null) throw new NotFoundException("Active incident not found in your branch.");

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


            var simulatedIncident = new BranchIncident
            {
                BranchId = incident.BranchId,
                Type = incident.Type,
                Scope = incident.Scope,
                StartedAtVn = incident.StartedAtVn,
                EstimatedEndAtVn = request.NewEstimatedEndAtVn,
                IncidentLanes = incident.IncidentLanes
            };

            incident.EstimatedEndAtVn = request.NewEstimatedEndAtVn;
            incident.UpdatedAtVn = now;
            incident.Version++;


            var newBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.BookingDetails)
                .Where(b => b.BranchId == incident.BranchId && b.UserId != null && (b.Status == "Pending" || b.Status == "Confirmed"))
                .Where(b => b.ScheduledTime >= oldEnd && b.ScheduledTime < request.NewEstimatedEndAtVn)
                .ToListAsync();

            var deadline = now.AddMinutes(30);
            var slots = await _context.TimeSlots.Where(s => s.BranchId == incident.BranchId).ToListAsync();
            var bookingsBySlot = newBookings.GroupBy(b => slots.FirstOrDefault(s => s.StartTime == b.ScheduledTime.TimeOfDay)?.SlotId ?? 0);

            foreach (var group in bookingsBySlot)
            {
                int slotId = group.Key;
                if (slotId == 0) continue;

                var slotBookings = group.OrderByDescending(b => b.BookingId).ToList(); // LIFO
                
                // Filter out bookings already affected
                var unaffectedSlotBookings = new List<Booking>();
                foreach (var b in slotBookings)
                {
                    if (!await _context.IncidentAffectedBookings.AnyAsync(c => c.BookingId == b.BookingId && c.IncidentId == incident.Id))
                    {
                        unaffectedSlotBookings.Add(b);
                    }
                }
                
                if (unaffectedSlotBookings.Count == 0) continue;
                
                var firstBooking = unaffectedSlotBookings.First();

                if (incident.Scope == "WholeBranch")
                {
                    foreach (var b in unaffectedSlotBookings)
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
                else
                {
                    var ctx = new BookingContextDTO
                    {
                        IsBusiness = firstBooking.BusinessProfileId.HasValue,
                        IsVipEligible = false, 
                        VehicleTypeId = firstBooking.Vehicle?.VehicleTypeId,
                        ServiceIds = firstBooking.BookingDetails.Select(d => d.ServiceId).ToList(),
                        CapacityWeight = 1
                    };

                    var cap = await _capacityService.GetEffectiveSlotCapacityAsync(incident.BranchId, firstBooking.ScheduledTime.Date, slotId, ctx, now, simulatedIncident);
                    
                    int effectiveCapacity = cap.EffectiveCapacity;
                    int overbookedAmount = cap.BookedWeight - effectiveCapacity;

                    if (overbookedAmount > 0)
                    {
                        foreach (var b in unaffectedSlotBookings)
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
                            int weight = b.CapacityWeight > 0 ? b.CapacityWeight : 1;
                            overbookedAmount -= weight;
                            if (overbookedAmount <= 0) break;
                        }
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
                .ThenInclude(b => b.BookingDetails)
                .Where(b => b.IncidentId == incident.Id && b.Status == "AwaitingCustomer")
                .ToListAsync();

            var slots = await _context.TimeSlots.Where(s => s.BranchId == incident.BranchId).ToListAsync();

            var casesToCancel = new List<AutoWashPro.DAL.Entities.IncidentAffectedBooking>();
            var slotBookedWeights = new Dictionary<int, int>();

            foreach (var c in pendingCases)
            {
                var b = c.Booking;
                var ctx = new BookingContextDTO
                {
                    IsBusiness = b.BusinessProfileId.HasValue,
                    IsVipEligible = false, 
                    VehicleTypeId = b.Vehicle?.VehicleTypeId,
                    ServiceIds = b.BookingDetails.Select(d => d.ServiceId).ToList(),
                    CapacityWeight = b.CapacityWeight > 0 ? b.CapacityWeight : 1
                };
                
                var slot = slots.FirstOrDefault(s => s.StartTime == b.ScheduledTime.TimeOfDay);
                int slotId = slot?.SlotId ?? 0;
                
                var cap = await _capacityService.GetEffectiveSlotCapacityAsync(incident.BranchId, b.ScheduledTime.Date, slotId, ctx, now, ignoreIncidentId: incident.Id);
                
                if (!slotBookedWeights.ContainsKey(slotId))
                {
                    slotBookedWeights[slotId] = cap.BookedWeight;
                }
                
                if (cap.EffectiveCapacity >= slotBookedWeights[slotId])
                {
                    c.Status = "Kept";
                    c.Decision = "Keep";
                    c.DecidedAtVn = now;
                    
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
                    slotBookedWeights[slotId] = Math.Max(0, slotBookedWeights[slotId] - ctx.CapacityWeight);

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
