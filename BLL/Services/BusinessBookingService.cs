using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class BusinessBookingService : IBusinessBookingService
    {
        private readonly AutoWashDbContext _context;
        private readonly ILaneSchedulerService _laneSchedulerService;
        private readonly IBookingMaterialUsageService _bookingMaterialUsageService;

        public BusinessBookingService(
            AutoWashDbContext context,
            ILaneSchedulerService laneSchedulerService,
            IBookingMaterialUsageService bookingMaterialUsageService)
        {
            _context = context;
            _laneSchedulerService = laneSchedulerService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
        }

        public async Task<List<DTOs.Business.TimeSlotResponseDTO>> GetAvailableSlotsForBusinessAsync(int businessUserId, CheckBusinessSlotsRequestDTO request)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x =>
                    x.UserId == businessUserId &&
                    x.ApprovalStatus == "Approved");

            if (business == null)
                throw new NotFoundException("Business profile not found or not approved.");

            var representativeVehicle = await _context.FleetVehicles
                .Include(x => x.VehicleType)
                .FirstOrDefaultAsync(x =>
                    x.FleetVehicleId == request.FleetVehicleId &&
                    x.BusinessProfileId == business.BusinessProfileId &&
                    x.Status == "Active");

            if (representativeVehicle == null)
                throw new NotFoundException("Vehicle not found or not activated.");

            // ── Timezone ─────────────────────────────────────────────────────
            TimeZoneInfo vnTimeZone;
            try { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

            DateTime todayInVN = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone).Date;
            TimeSpan currentTimeInVN = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone).TimeOfDay;

            if (request.TargetDate.Date < todayInVN)
                throw new BadRequestException("Cannot book for a date in the past.");

            // ── Build schedule requests for N vehicles ────────────────────────
            // All vehicles in a fleet booking share the same type and services,
            // so we repeat the representative vehicle N times for the simulation.
            var servicePrices = await _context.ServicePrices
                .Where(x =>
                    x.BranchId == request.BranchId &&
                    x.VehicleTypeId == representativeVehicle.VehicleTypeId &&
                    request.ServiceIds.Contains(x.ServiceId))
                .ToListAsync();

            // If no services selected yet fall back to base weight only (conservative estimate)
            if (!servicePrices.Any() && request.ServiceIds.Any())
                throw new BadRequestException("One or more services do not exist or are not priced.");

            var simRequests = Enumerable.Range(0, (int)request.VehicleCount)
                .Select(_ => new VehicleScheduleRequest
                {
                    FleetVehicleId = 0, // simulation — no real ID needed
                    VehicleType = representativeVehicle.VehicleType,
                    ServicePrices = servicePrices
                })
                .ToList();

            // ── Check each slot ───────────────────────────────────────────────
            var allSlots = await _context.TimeSlots
                .Where(s => s.BranchId == request.BranchId)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            var response = new List<DTOs.Business.TimeSlotResponseDTO>();

            foreach (var slot in allSlots)
            {
                var slotDto = new DTOs.Business.TimeSlotResponseDTO
                {
                    SlotId = slot.SlotId,
                    TimeRange = $"{slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm}",
                    IsAvailable = true,
                    Reason = "Available"
                };

                // Past-time guard
                if (request.TargetDate.Date == todayInVN && slot.StartTime < currentTimeInVN)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = "Past time";
                    response.Add(slotDto);
                    continue;
                }

                DateTime slotStart = request.TargetDate.Date.Add(slot.StartTime);
                TimeSpan slotDuration = slot.EndTime - slot.StartTime;

                var simResult = await _laneSchedulerService.ScheduleFleetAsync(
                    request.BranchId, slotStart, slotDuration, simRequests);

                if (!simResult.Success)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = simResult.ErrorMessage!;
                }
                else
                {
                    // Show how deep into the slot the last vehicle finishes (kept for compat)
                    var lastEnd = simResult.Assignments.Max(a => a.EstimatedEnd);
                    slotDto.EstimatedLastEndMinutesIntoSlot =
                        (int)(lastEnd - slotStart).TotalMinutes;

                    // NEW: per-vehicle projected start/end times, so Business B sees
                    // exactly when each of their vehicles will actually check in —
                    // already accounting for Business A's earlier confirmed occupancy
                    // on the same lanes (handled inside ScheduleFleetAsync's lookup).
                    var laneIds = simResult.Assignments.Select(a => a.LaneId).Distinct().ToList();
                    var laneNames = await _context.Lanes
                        .Where(x => laneIds.Contains(x.LaneId))
                        .ToDictionaryAsync(x => x.LaneId, x => x.Name);

                    slotDto.VehicleProjections = simResult.Assignments
                        .Select(a => new VehicleSlotProjectionDTO
                        {
                            FleetVehicleId = a.FleetVehicleId,
                            EstimatedStart = a.EstimatedStart,
                            EstimatedEnd = a.EstimatedEnd,
                            LaneName = laneNames.TryGetValue(a.LaneId, out var ln) ? ln : ""
                        })
                        .ToList();
                }

                response.Add(slotDto);
            }

            return response;
        }

        public async Task<MultiVehicleBookingResponseDTO> CreateBusinessBookingAsync(int businessUserId, CreateBusinessBookingDTO dto)
        {
            // ── Validate business ─────────────────────────────────────────────
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x =>
                    x.UserId == businessUserId &&
                    x.ApprovalStatus == "Approved");

            if (business == null)
                throw new NotFoundException("Business profile not found.");

            // ── Validate all fleet vehicles belong to this business ───────────
            var vehicleIds = dto.Vehicles.Select(v => v.FleetVehicleId).ToList();

            var fleetVehicles = await _context.FleetVehicles
                .Include(x => x.VehicleType)
                .Where(x =>
                    vehicleIds.Contains(x.FleetVehicleId) &&
                    x.BusinessProfileId == business.BusinessProfileId)
                .ToListAsync();

            if (fleetVehicles.Count != dto.Vehicles.Count)
                throw new NotFoundException("One or more vehicles do not belong to this business.");

            var inactiveVehicle = fleetVehicles.FirstOrDefault(x => x.Status != "Active");
            if (inactiveVehicle != null)
                throw new BadRequestException(
                    $"Vehicle {inactiveVehicle.LicensePlate} is not activated.");

            // ── Validate branch + slot ────────────────────────────────────────
            var branch = await _context.Branches
                .FirstOrDefaultAsync(x => x.BranchId == dto.BranchId);

            if (branch == null)
                throw new NotFoundException("Branch not found.");

            var slot = await _context.TimeSlots
                .FirstOrDefaultAsync(x =>
                    x.SlotId == dto.SlotId &&
                    x.BranchId == dto.BranchId);

            if (slot == null)
                throw new NotFoundException("Time slot not found.");

            DateTime scheduledTime = dto.ScheduledTime.Date.Add(slot.StartTime);
            TimeSpan slotDuration = slot.EndTime - slot.StartTime;

            // ── Build per-vehicle schedule requests ───────────────────────────
            var scheduleRequests = new List<VehicleScheduleRequest>();

            foreach (var item in dto.Vehicles)
            {
                var vehicle = fleetVehicles.First(v => v.FleetVehicleId == item.FleetVehicleId);

                if (!item.ServiceIds.Any())
                    throw new BadRequestException(
                        $"Vehicle {vehicle.LicensePlate} must have at least one service.");

                var vehicleServicePrices = await _context.ServicePrices
                    .Where(sp =>
                        sp.BranchId == dto.BranchId &&
                        sp.VehicleTypeId == vehicle.VehicleTypeId &&
                        item.ServiceIds.Contains(sp.ServiceId))
                    .ToListAsync();

                if (vehicleServicePrices.Count != item.ServiceIds.Count)
                    throw new BadRequestException(
                        $"One or more services have not been priced for the vehicle " +
                        $"{vehicle.LicensePlate} ({vehicle.VehicleType.Name}).");

                scheduleRequests.Add(new VehicleScheduleRequest
                {
                    FleetVehicleId = vehicle.FleetVehicleId,
                    VehicleType = vehicle.VehicleType,
                    ServicePrices = vehicleServicePrices
                });
            }

            // ── Run EAL simulation ────────────────────────────────────────────
            var scheduleResult = await _laneSchedulerService.ScheduleFleetAsync(
                dto.BranchId, scheduledTime, slotDuration, scheduleRequests);

            if (!scheduleResult.Success)
                throw new BadRequestException(scheduleResult.ErrorMessage!);

            // ── Load lane names for response ──────────────────────────────────
            var laneIds = scheduleResult.Assignments.Select(a => a.LaneId).Distinct().ToList();
            var laneNames = await _context.Lanes
                .Where(x => laneIds.Contains(x.LaneId))
                .ToDictionaryAsync(x => x.LaneId, x => x.Name);

            // ── Load or create DailySlotCapacity ONCE before the loop ─────────
            // Loading inside the loop causes duplicate inserts because EF's
            // change tracker doesn't see the unsaved first insert on subsequent
            // iterations, resulting in a unique constraint violation on
            // IX_DailySlotCapacities_SlotId_Date_BranchId.
            var dailyCapacity = await _context.DailySlotCapacities
                .FirstOrDefaultAsync(x =>
                    x.BranchId == dto.BranchId &&
                    x.SlotId == dto.SlotId &&
                    x.Date == dto.ScheduledTime.Date);

            if (dailyCapacity == null)
            {
                dailyCapacity = new DailySlotCapacity
                {
                    BranchId = dto.BranchId,
                    SlotId = dto.SlotId,
                    Date = dto.ScheduledTime.Date,
                    BookedWeight = 0
                };
                _context.DailySlotCapacities.Add(dailyCapacity);
            }

            // ── Persist: one Booking + BookingDetails per vehicle ─────────────
            var vehicleSummaries = new List<VehicleBookingSummaryDTO>();
            decimal totalAmount = 0;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var item in dto.Vehicles)
                {
                    var vehicle = fleetVehicles.First(v => v.FleetVehicleId == item.FleetVehicleId);
                    var request = scheduleRequests.First(r => r.FleetVehicleId == item.FleetVehicleId);
                    var assignment = scheduleResult.Assignments.First(a => a.FleetVehicleId == item.FleetVehicleId);

                    decimal vehicleTotal = request.ServicePrices.Sum(sp => sp.Price);

                    // Capacity check — accumulated across all vehicles in this request
                    if (dailyCapacity.BookedWeight + vehicle.VehicleType.BaseWeight > slot.MaxCapacity)
                        throw new BadRequestException(
                            $"The time slot is fully booked for vehicle {vehicle.LicensePlate}.");

                    dailyCapacity.BookedWeight += vehicle.VehicleType.BaseWeight;

                    var booking = new Booking
                    {
                        UserId = business.UserId,
                        BusinessProfileId = business.BusinessProfileId,
                        FleetVehicleId = vehicle.FleetVehicleId,
                        BookingType = "Business",
                        BranchId = dto.BranchId,
                        ScheduledTime = scheduledTime,
                        LicensePlate = vehicle.LicensePlate,
                        Status = "Pending",
                        OriginalPrice = vehicleTotal,
                        FinalAmount = vehicleTotal,
                        CapacityWeight = vehicle.VehicleType.BaseWeight,
                        ActualVehicleTypeId = vehicle.VehicleTypeId,
                        FallbackQrCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                        ProcessingLaneId = assignment.LaneId
                    };

                    _context.Bookings.Add(booking);

                    foreach (var sp in request.ServicePrices)
                    {
                        _context.BookingDetails.Add(new BookingDetail
                        {
                            Booking = booking,
                            ServiceId = sp.ServiceId,
                            Price = sp.Price
                        });
                    }

                    totalAmount += vehicleTotal;

                    vehicleSummaries.Add(new VehicleBookingSummaryDTO
                    {
                        LicensePlate = vehicle.LicensePlate,
                        LaneId = assignment.LaneId,
                        LaneName = laneNames.TryGetValue(assignment.LaneId, out var ln) ? ln : "",
                        EstimatedStart = assignment.EstimatedStart,
                        EstimatedEnd = assignment.EstimatedEnd,
                        Amount = vehicleTotal
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // ── Back-fill BookingIds after EF assigns them ────────────────────
            var savedBookings = await _context.Bookings
                .Where(x =>
                    x.BusinessProfileId == business.BusinessProfileId &&
                    x.ScheduledTime == scheduledTime &&
                    x.Status == "Pending")
                .OrderBy(x => x.BookingId)
                .Select(x => new { x.BookingId, x.LicensePlate })
                .ToListAsync();

            foreach (var summary in vehicleSummaries)
            {
                var match = savedBookings.FirstOrDefault(b => b.LicensePlate == summary.LicensePlate);
                if (match != null)
                    summary.BookingId = match.BookingId;
            }

            return new MultiVehicleBookingResponseDTO
            {
                BookingGroupId = vehicleSummaries.First().BookingId,
                TotalVehicles = vehicleSummaries.Count,
                TotalAmount = totalAmount,
                Status = "Pending",
                Vehicles = vehicleSummaries
            };
        }

        public async Task<RescheduleBusinessResponseDTO> RescheduleBookingAsync(int businessUserId, DTOs.Business.RescheduleBusinessBookingDTO dto)
        {
            // ── Validate business + booking ownership ─────────────────────────
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
                throw new NotFoundException("Business profile not found.");

            var booking = await _context.Bookings
                .Include(x => x.FleetVehicle)
                    .ThenInclude(x => x!.VehicleType)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == dto.BookingId &&
                    x.BusinessProfileId == business.BusinessProfileId);

            if (booking == null)
                throw new NotFoundException("Booking not found.");

            if (booking.Status != "Pending")
                throw new BadRequestException("Can only reschedule bookings in pending status.");

            // ── 24h cutoff check ───────────────────────────────────────────────
            // Business cannot reschedule once we're within 24h of the original slot start.
            if (booking.ScheduledTime <= DateTime.UtcNow.AddHours(24))
                throw new BadRequestException(
                    "Cannot reschedule within 24 hours of the appointment time. " +
                    "Please contact the branch for support.");

            // ── Validate new slot exists at the same branch ──────────────────
            var newSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(x =>
                    x.SlotId == dto.NewSlotId &&
                    x.BranchId == booking.BranchId);

            if (newSlot == null)
                throw new NotFoundException("New time slot not found.");

            DateTime newScheduledTime = dto.NewScheduledDate.Date.Add(newSlot.StartTime);
            TimeSpan newSlotDuration = newSlot.EndTime - newSlot.StartTime;

            // New target also can't be in the past, and ideally should itself be
            // outside the 24h window for the SAME reason — prevents using reschedule
            // to dodge the cutoff by jumping into a near slot.
            if (newScheduledTime <= DateTime.UtcNow.AddHours(24))
                throw new BadRequestException(
                    "The new time slot must be at least 24 hours from the current time.");

            bool isSameSlot =
                booking.ScheduledTime == newScheduledTime;

            if (isSameSlot)
                throw new BadRequestException("The new time slot is identical to the current time slot.");

            // ── Get this vehicle's service prices (same services as original booking) ──
            var bookingDetails = await _context.BookingDetails
                .Where(x => x.BookingId == booking.BookingId)
                .ToListAsync();

            var serviceIds = bookingDetails.Select(x => x.ServiceId).ToList();

            var servicePrices = await _context.ServicePrices
                .Where(sp =>
                    sp.BranchId == booking.BranchId &&
                    sp.VehicleTypeId == booking.FleetVehicle!.VehicleTypeId &&
                    serviceIds.Contains(sp.ServiceId))
                .ToListAsync();

            // ── Run EAL against the NEW slot, EXCLUDING this booking's own current
            //    occupancy (since it's being moved, not duplicated) ─────────────
            var scheduleRequest = new List<VehicleScheduleRequest>
    {
        new VehicleScheduleRequest
        {
            FleetVehicleId = booking.FleetVehicleId!.Value,
            VehicleType    = booking.FleetVehicle!.VehicleType,
            ServicePrices  = servicePrices
        }
    };

            var scheduleResult = await _laneSchedulerService.ScheduleFleetAsync(
                booking.BranchId, newScheduledTime, newSlotDuration, scheduleRequest);

            if (!scheduleResult.Success)
                throw new BadRequestException(scheduleResult.ErrorMessage!);

            var newAssignment = scheduleResult.Assignments.First();

            var newLane = await _context.Lanes
                .FirstOrDefaultAsync(x => x.LaneId == newAssignment.LaneId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ── Release OLD slot capacity ────────────────────────────────────
                var oldSlot = await _context.TimeSlots
                    .FirstOrDefaultAsync(x =>
                        x.BranchId == booking.BranchId &&
                        x.StartTime == booking.ScheduledTime.TimeOfDay);

                if (oldSlot != null)
                {
                    var oldDailyCapacity = await _context.DailySlotCapacities
                        .FirstOrDefaultAsync(x =>
                            x.BranchId == booking.BranchId &&
                            x.SlotId == oldSlot.SlotId &&
                            x.Date == booking.ScheduledTime.Date);

                    if (oldDailyCapacity != null)
                    {
                        oldDailyCapacity.BookedWeight -= booking.CapacityWeight;
                        if (oldDailyCapacity.BookedWeight < 0)
                            oldDailyCapacity.BookedWeight = 0;
                    }
                }

                // ── Reserve NEW slot capacity ─────────────────────────────────────
                var newDailyCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(x =>
                        x.BranchId == booking.BranchId &&
                        x.SlotId == newSlot.SlotId &&
                        x.Date == newScheduledTime.Date);

                if (newDailyCapacity == null)
                {
                    newDailyCapacity = new DailySlotCapacity
                    {
                        BranchId = booking.BranchId,
                        SlotId = newSlot.SlotId,
                        Date = newScheduledTime.Date,
                        BookedWeight = 0
                    };
                    _context.DailySlotCapacities.Add(newDailyCapacity);
                }

                if (newDailyCapacity.BookedWeight + booking.CapacityWeight > newSlot.MaxCapacity)
                    throw new BadRequestException("The new time slot is fully booked.");

                newDailyCapacity.BookedWeight += booking.CapacityWeight;

                // ── Update booking itself ─────────────────────────────────────────
                DateTime oldScheduledTime = booking.ScheduledTime;

                booking.ScheduledTime = newScheduledTime;
                booking.ProcessingLaneId = newAssignment.LaneId;  // re-reserve new lane
                booking.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new RescheduleBusinessResponseDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate,
                    OldScheduledTime = oldScheduledTime,
                    NewScheduledTime = newScheduledTime,
                    LaneId = newAssignment.LaneId,
                    LaneName = newLane?.Name ?? "",
                    EstimatedStart = newAssignment.EstimatedStart,
                    EstimatedEnd = newAssignment.EstimatedEnd
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<FleetVehicleDTO>> GetActiveFleetVehiclesAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            return await _context.FleetVehicles
                .Include(x => x.VehicleType)
                .Where(x =>
                    x.BusinessProfileId == business.BusinessProfileId &&
                    x.Status == "Active")
                .Select(x => new FleetVehicleDTO
                {
                    FleetVehicleId = x.FleetVehicleId,
                    LicensePlate = x.LicensePlate,
                    Brand = x.Brand,
                    Model = x.Model,
                    VehicleTypeName = x.VehicleType.Name,
                    DriverName = x.DriverName,
                    EmployeeId = x.EmployeeCode,
                    Status = x.Status
                })
                .ToListAsync();
        }

        public async Task<List<BusinessBookingListDTO>> GetBookingsAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            return await _context.Bookings
                .Where(x =>
                    x.BusinessProfileId ==
                    business.BusinessProfileId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new BusinessBookingListDTO
                {
                    BookingId = x.BookingId,
                    LicensePlate = x.LicensePlate,
                    ScheduledTime = x.ScheduledTime,
                    Status = x.Status,
                    FinalAmount = x.FinalAmount
                })
                .ToListAsync();
        }

        public async Task<BusinessBookingDetailDTO> GetBookingDetailAsync(int businessUserId, int bookingId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }

            var booking = await _context.Bookings
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Service)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == bookingId &&
                    x.BusinessProfileId == business.BusinessProfileId);

            if (booking == null)
            {
                throw new NotFoundException("Booking not found.");
            }

            return new BusinessBookingDetailDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = booking.LicensePlate,
                ScheduledTime = booking.ScheduledTime,
                Status = booking.Status,
                OriginalPrice = booking.OriginalPrice,
                FinalAmount = booking.FinalAmount,
                Services = booking.BookingDetails
                    .Select(x => x.Service.ServiceName)
                    .ToList()
            };
        }

        public async Task CancelBookingAsync(int businessUserId, int bookingId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(x =>
                    x.BookingId == bookingId &&
                    x.BusinessProfileId == business.BusinessProfileId);

            if (booking == null)
            {
                throw new NotFoundException("Booking not found.");
            }

            if (booking.Status != "Pending")
            {
                throw new BadRequestException("Can only cancel bookings in pending status.");
            }

            var slot = await _context.TimeSlots
                .FirstOrDefaultAsync(x =>
                    x.BranchId == booking.BranchId &&
                    x.StartTime == booking.ScheduledTime.TimeOfDay);

            if (slot != null)
            {
                var dailyCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(x =>
                        x.BranchId == booking.BranchId &&
                        x.SlotId == slot.SlotId &&
                        x.Date == booking.ScheduledTime.Date);

                if (dailyCapacity != null)
                {
                    dailyCapacity.BookedWeight -= booking.CapacityWeight;

                    if (dailyCapacity.BookedWeight < 0)
                    {
                        dailyCapacity.BookedWeight = 0;
                    }
                }
            }

            booking.Status = "Cancelled";
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<FleetWashLogDTO> CheckInAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(x => x.FleetVehicle)
                .Include(x => x.BookingDetails)
                .FirstOrDefaultAsync(x =>
                    x.BookingId == bookingId);

            if (booking == null) throw new NotFoundException("Booking not found.");

            if (booking.BookingType != "Business") throw new BadRequestException("This is not a business booking.");

            if (booking.Status != "Pending") throw new BadRequestException("This booking cannot be checked in yet.");

            var detail = booking.BookingDetails.First();

            var washLog = new FleetWashLog
            {
                FleetVehicleId = booking.FleetVehicleId!.Value,
                BranchId = booking.BranchId,
                BookingId = booking.BookingId,
                CheckInTime = DateTime.UtcNow,
                WashCost = booking.FinalAmount,
                Status = "CheckedIn"
            };

            _context.FleetWashLogs.Add(washLog);

            booking.Status = "CheckedIn";

            await _context.SaveChangesAsync();

            return new FleetWashLogDTO
            {
                FleetWashLogId = washLog.FleetWashLogId,
                LicensePlate = booking.LicensePlate,
                CheckInTime = washLog.CheckInTime,
                Status = washLog.Status
            };
        }

        public async Task<FleetCheckInResponseDTO> WalkInAsync(FleetWalkInDTO dto)
        {
            var vehicle = await _context.FleetVehicles
                .FirstOrDefaultAsync(x =>
                    x.LicensePlate == dto.LicensePLate &&
                    x.Status == "Active");

            if (vehicle == null)
            {
                throw new NotFoundException("Vehicle not found in the fleet.");
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(x =>
                    x.BranchId == dto.BranchId);

            if (branch == null)
            {
                throw new NotFoundException("Branch not found.");
            }

            var existingLog = await _context.FleetWashLogs
                .FirstOrDefaultAsync(x =>
                    x.FleetVehicleId == vehicle.FleetVehicleId &&
                    (x.Status == "CheckedIn" ||
                     x.Status == "Processing"));

            if (existingLog != null)
            {
                throw new BadRequestException("This vehicle is currently undergoing wash processing.");
            }

            var washLog = new FleetWashLog
            {
                FleetVehicleId = vehicle.FleetVehicleId,
                BranchId = dto.BranchId,
                BookingId = null,
                CheckInTime = DateTime.UtcNow,
                Status = "CheckedIn",
                WashCost = 0
            };

            _context.FleetWashLogs.Add(washLog);

            await _context.SaveChangesAsync();

            return new FleetCheckInResponseDTO
            {
                FleetWashLogId = washLog.FleetWashLogId,
                FleetVehicleId = vehicle.FleetVehicleId,
                LicensePlate = vehicle.LicensePlate,
                DriverName = vehicle.DriverName,
                CheckInTime = washLog.CheckInTime,
                Status = washLog.Status!
            };
        }

        public async Task WalkOutAsync(int washLogId)
        {
            var washLog = await _context.FleetWashLogs
                .FirstOrDefaultAsync(x => x.FleetWashLogId == washLogId);

            if (washLog == null)
            {
                throw new NotFoundException("Car wash log not found.");
            }

            if (washLog.Status != "Processing")
            {
                throw new BadRequestException("Vehicle must be in processing status.");
            }

            washLog.Status = "Completed";
            washLog.CompletedTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task StartProcessingAsync(int washLogId, int staffUserId, StartFleetWashDTO dto)
        {
            var washLog = await _context.FleetWashLogs
                .Include(x => x.Booking)
                .FirstOrDefaultAsync(x =>
                    x.FleetWashLogId == washLogId);

            if (washLog == null)
            {
                throw new NotFoundException("Car wash log not found.");
            }

            if (washLog.Status != "Assigned")
            {
                throw new BadRequestException("Vehicle is not in pending processing status.");
            }

            var lane = await _context.Lanes
                .FirstOrDefaultAsync(x => x.LaneId == dto.LaneId);

            if (lane == null)
            {
                throw new NotFoundException("Wash lane not found.");
            }

            washLog.Status = "Processing";

            if (washLog.Booking != null)
            {
                washLog.Booking.ProcessingLaneId = dto.LaneId;
                washLog.Booking.ProcessingStaffId = staffUserId;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<CurrentFleetVehicleDTO>> GetCurrentVehiclesAsync()
        {
            return await _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                .Where(x =>
                    x.Status == "CheckedIn" ||
                    x.Status == "Processing")
                .OrderBy(x => x.CheckInTime)
                .Select(x => new CurrentFleetVehicleDTO
                {
                    FleetWashLogId = x.FleetWashLogId,
                    LicensePlate = x.FleetVehicle.LicensePlate,
                    DriverName = x.FleetVehicle.DriverName,
                    Status = x.Status!,
                    CheckInTime = x.CheckInTime
                })
                .ToListAsync();
        }

        public async Task<FleetCheckoutResponseDTO> CheckOutAsync(int washLogId)
        {
            var washLog = await _context.FleetWashLogs
                .Include(x => x.Booking)
                    .ThenInclude(x => x.BookingDetails)
                .FirstOrDefaultAsync(x =>
                    x.FleetWashLogId == washLogId &&
                    x.Status != "Completed");

            if (washLog == null)
            {
                throw new NotFoundException("Car wash log not found.");
            }

            if (washLog.BookingId.HasValue)
            {
                var booking = washLog.Booking!;

            }

            if (washLog.Status != "Processing")
            {
                throw new BadRequestException("Can only check out vehicles that are currently in processing status.");
            }

            washLog.Status = "Completed";
            washLog.CompletedTime = DateTime.UtcNow;

            if (washLog.Booking != null)
            {
                washLog.Booking.Status = "Completed";
                washLog.Booking.UpdatedAt = DateTime.UtcNow;
            }

            if (washLog.BookingId.HasValue)
            {
                await _bookingMaterialUsageService.ConsumeForCompletedBookingAsync(washLog.BookingId.Value);
            }

            await _context.SaveChangesAsync();

            return new FleetCheckoutResponseDTO
            {
                FleetWashLogId = washLog.FleetWashLogId,
                CompletedTime = washLog.CompletedTime.Value
            };
        }

        public async Task<InvoiceDTO> GetInvoiceByBookingAsync(int bookingId)
        {
            var invoice = await _context.Invoices
                .Include(x => x.InvoiceItems)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (invoice == null)
                throw new NotFoundException("Invoice not found.");

            return new InvoiceDTO
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceCode = invoice.InvoiceCode,
                Subtotal = invoice.Subtotal,
                TaxAmount = invoice.TaxAmount,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status,
                IssuedAt = invoice.IssuedAt,
                Items = invoice.InvoiceItems.Select(x => new InvoiceItemDTO
                {
                    Description = x.Description,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    Amount = x.Amount
                }).ToList()
            };
        }

        public async Task<List<FleetWashHistoryDTO>> GetFleetWashHistoryAsync(int businessUserId, FleetHistoryFilterDTO filter)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            var query = _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                    .ThenInclude(x => x.VehicleType)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.Branch)
                .Where(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId)
                .AsQueryable();

            if (filter.FleetVehicleId.HasValue)
            {
                query = query.Where(x => x.FleetVehicleId == filter.FleetVehicleId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x => x.CheckInTime >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x => x.CheckInTime <= filter.ToDate.Value);
            }

            return await query
                .OrderByDescending(x => x.CheckInTime)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new FleetWashHistoryDTO
                {
                    FleetWashLogId = x.FleetWashLogId,
                    LicensePlate = x.FleetVehicle.LicensePlate,
                    VehicleType = x.FleetVehicle.VehicleType.Name,
                    BranchName =
                        x.Booking != null
                            ? x.Booking.Branch.Name
                            : "Walk-In",
                    CheckInTime = x.CheckInTime,
                    CompletedTime = x.CompletedTime,
                    Status = x.Status!,
                    WashCost = x.WashCost,
                    BookingId = x.BookingId,
                    WashType =
                        x.BookingId != null
                            ? "Booking"
                            : "WalkIn"
                })
                .ToListAsync();
        }

        public async Task<FleetDashboardDTO> GetDashboardAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            var today = DateTime.Today;

            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            var vehicleIds = await _context.FleetVehicles
                .Where(x => x.BusinessProfileId == business.BusinessProfileId)
                .Select(x => x.FleetVehicleId)
                .ToListAsync();

            return new FleetDashboardDTO
            {
                TotalVehicles = await _context.FleetVehicles
                    .CountAsync(x => x.BusinessProfileId == business.BusinessProfileId),

                ActiveVehicles = await _context.FleetVehicles
                    .CountAsync(x => x.BusinessProfileId == business.BusinessProfileId && x.Status == "Active"),

                PendingVehicles = await _context.FleetVehicles
                    .CountAsync(x => x.BusinessProfileId == business.BusinessProfileId && x.Status == "PendingApproval"),

                TodayWashCount = await _context.FleetWashLogs
                    .CountAsync(x => vehicleIds.Contains(x.FleetVehicleId) && x.CheckInTime.Date == today),

                MonthlyWashCount = await _context.FleetWashLogs
                    .CountAsync(x => vehicleIds.Contains(x.FleetVehicleId) && x.CheckInTime >= firstDayOfMonth),

                MonthlySpend = await _context.FleetWashLogs
                        .Where(x => vehicleIds.Contains(x.FleetVehicleId) && x.CheckInTime >= firstDayOfMonth)
                        .SumAsync(x => (decimal?)x.WashCost) ?? 0,

                VehiclesCurrentlyInStation = await _context.FleetWashLogs
                    .CountAsync(x => vehicleIds.Contains(x.FleetVehicleId) && x.Status != "Completed" && x.Status != "Cancelled")
            };
        }

        public async Task<List<InvoiceListDTO>> GetInvoicesAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            return await _context.Invoices
                .Include(x => x.Booking)
                .Where(x => x.BusinessProfileId == business.BusinessProfileId)
                .OrderByDescending(x => x.IssuedAt)
                .Select(x => new InvoiceListDTO
                {
                    InvoiceId = x.InvoiceId,
                    InvoiceCode = x.InvoiceCode,
                    IssuedAt = x.IssuedAt,
                    TotalAmount = x.TotalAmount,
                    Status = x.Status,
                    LicensePlate = x.Booking.LicensePlate
                })
                .ToListAsync();
        }

        public async Task<InvoiceDetailDTO> GetInvoiceDetailAsync(int businessUserId, int invoiceId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            var invoice = await _context.Invoices
                .Include(x => x.Booking)
                .Include(x => x.InvoiceItems)
                .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId && x.BusinessProfileId == business.BusinessProfileId);

            if (invoice == null) throw new NotFoundException("Invoice not found.");

            return new InvoiceDetailDTO
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceCode = invoice.InvoiceCode,
                IssuedAt = invoice.IssuedAt,
                Subtotal = invoice.Subtotal,
                TaxAmount = invoice.TaxAmount,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status,
                LicensePlate = invoice.Booking.LicensePlate,

                Items = invoice.InvoiceItems
                    .Select(i => new InvoiceItemDTO
                    {
                        InvoiceItemId = i.InvoiceItemId,
                        Description = i.Description,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        Amount = i.Amount
                    })
                    .ToList()
            };
        }

        public async Task<MonthlyStatementDTO> GetMonthlyStatementAsync(int businessUserId, int year, int month)
        {
            var business = await _context.BusinessProfiles.FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null) throw new NotFoundException("Business profile not found.");

            var startDate = new DateTime(year, month, 1);

            var endDate = startDate.AddMonths(1);

            var logs = await _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                .Where(x =>
                    x.FleetVehicle.BusinessProfileId ==
                    business.BusinessProfileId &&
                    x.CheckInTime >= startDate &&
                    x.CheckInTime < endDate)
                .ToListAsync();

            return new MonthlyStatementDTO
            {
                Year = year,
                Month = month,
                TotalWashes = logs.Count,
                TotalCost = logs.Sum(x => x.WashCost),
                Vehicles = logs
                    .GroupBy(x => new
                    {
                        x.FleetVehicleId,
                        x.FleetVehicle.LicensePlate
                    })
                    .Select(g => new VehicleStatementDTO
                    {
                        FleetVehicleId = g.Key.FleetVehicleId,
                        LicensePlate = g.Key.LicensePlate,
                        WashCount = g.Count(),
                        TotalCost = g.Sum(x => x.WashCost)
                    })
                    .OrderByDescending(x => x.TotalCost)
                    .ToList()
            };
        }

        public async Task AssignLaneAsync(int washLogId, AssignLaneDTO dto)
        {
            var washLog = await _context.FleetWashLogs.FirstOrDefaultAsync(x => x.FleetWashLogId == washLogId);

            if (washLog == null)
            {
                throw new NotFoundException("Car wash log not found.");
            }

            if (washLog.Status != "CheckedIn")
            {
                throw new BadRequestException("Vehicle is not in pending assignment status.");
            }

            var lane = await _context.Lanes.FirstOrDefaultAsync(x => x.LaneId == dto.LaneId);

            if (lane == null)
            {
                throw new NotFoundException("Wash lane not found.");
            }

            var staff = await _context.Users.FirstOrDefaultAsync(x => x.UserId == dto.StaffUserId);

            if (staff == null)
            {
                throw new NotFoundException("Employee not found.");
            }

            washLog.LaneId = dto.LaneId;
            washLog.StaffUserId = dto.StaffUserId;
            washLog.Status = "Assigned";

            await _context.SaveChangesAsync();
        }

        public async Task<List<BusinessVehicleStatusDTO>> GetActiveVehiclesOnFloorAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
                throw new NotFoundException("Business profile not found.");

            var result = new List<BusinessVehicleStatusDTO>();

            // ── 1. Pending / Cancelled Bookings ───────────────────────────
            var bookings = await _context.Bookings
                .Include(x => x.FleetVehicle)
                    .ThenInclude(x => x.VehicleType)
                .Include(x => x.Branch)
                .Where(x =>
                    x.BusinessProfileId == business.BusinessProfileId &&
                    x.BookingType == "Business" &&
                    (x.Status == "Pending" || x.Status == "Cancelled"))
                .OrderBy(x => x.ScheduledTime)
                .ToListAsync();

            result.AddRange(bookings.Select(x => new BusinessVehicleStatusDTO
            {
                FleetWashLogId = null,
                BookingId = x.BookingId,
                LicensePlate = x.LicensePlate,
                DriverName = x.FleetVehicle!.DriverName,
                VehicleType = x.FleetVehicle.VehicleType.Name,
                Status = x.Status,
                WashType = "Booking",
                LaneName = null,
                BranchName = x.Branch.Name,
                ScheduledTime = x.ScheduledTime,
                CheckInTime = null,
                CompletedTime = null,
                WashCost = x.FinalAmount
            }));

            // ── 2. Active Wash Logs ───────────────────────────────────────
            var washLogs = await _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                    .ThenInclude(x => x.VehicleType)
                .Include(x => x.Lane)
                .Where(x =>
                    x.FleetVehicle.BusinessProfileId == business.BusinessProfileId &&
                    (x.Status == "CheckedIn" ||
                     x.Status == "Assigned" ||
                     x.Status == "Processing"))
                .OrderBy(x => x.CheckInTime)
                .ToListAsync();

            result.AddRange(washLogs.Select(x => new BusinessVehicleStatusDTO
            {
                FleetWashLogId = x.FleetWashLogId,
                BookingId = x.BookingId,
                LicensePlate = x.FleetVehicle.LicensePlate,
                DriverName = x.FleetVehicle.DriverName,
                VehicleType = x.FleetVehicle.VehicleType.Name,
                Status = x.Status!,
                WashType = x.BookingId != null ? "Booking" : "WalkIn",
                LaneName = x.Lane?.Name,
                BranchName = null,
                ScheduledTime = null,
                CheckInTime = x.CheckInTime,
                CompletedTime = x.CompletedTime,
                WashCost = x.WashCost
            }));

            return result
                .OrderBy(x => x.Status == "Pending" || x.Status == "Cancelled" ? 0 : 1)
                .ThenBy(x => x.ScheduledTime ?? x.CheckInTime)
                .ToList();
        }

        public async Task<List<BusinessVehicleStatusDTO>> GetVehiclesByStatusAsync(int businessUserId, string? status)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
                throw new NotFoundException("Business profile not found.");

            var result = new List<BusinessVehicleStatusDTO>();

            // ── 1. Bookings not yet checked in (Pending, Cancelled) ───────────
            bool includePending = string.IsNullOrWhiteSpace(status)
                || status == "Pending"
                || status == "Cancelled";

            if (includePending)
            {
                var bookingQuery = _context.Bookings
                    .Include(x => x.FleetVehicle)
                        .ThenInclude(x => x.VehicleType)
                    .Include(x => x.Branch)
                    .Where(x =>
                        x.BusinessProfileId == business.BusinessProfileId &&
                        x.BookingType == "Business" &&
                        (x.Status == "Pending" || x.Status == "Cancelled"));

                if (!string.IsNullOrWhiteSpace(status))
                    bookingQuery = bookingQuery.Where(x => x.Status == status);

                var bookings = await bookingQuery
                    .OrderByDescending(x => x.ScheduledTime)
                    .ToListAsync();

                result.AddRange(bookings.Select(x => new BusinessVehicleStatusDTO
                {
                    FleetWashLogId = null,
                    BookingId = x.BookingId,
                    LicensePlate = x.LicensePlate,
                    DriverName = x.FleetVehicle!.DriverName,
                    VehicleType = x.FleetVehicle.VehicleType.Name,
                    Status = x.Status,
                    WashType = "Booking",
                    LaneName = null,
                    BranchName = x.Branch.Name,
                    ScheduledTime = x.ScheduledTime,
                    CheckInTime = null,
                    CompletedTime = null,
                    WashCost = x.FinalAmount
                }));
            }

            // ── 2. WashLogs (CheckedIn, Assigned, Processing, Completed) ─────
            bool includeWashLog = string.IsNullOrWhiteSpace(status)
                || status is "CheckedIn" or "Assigned" or "Processing" or "Completed";

            if (includeWashLog)
            {
                var logQuery = _context.FleetWashLogs
                    .Include(x => x.FleetVehicle)
                        .ThenInclude(x => x.VehicleType)
                    .Include(x => x.Lane)
                    .Where(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId);

                if (!string.IsNullOrWhiteSpace(status))
                    logQuery = logQuery.Where(x => x.Status == status);

                var logs = await logQuery
                    .OrderByDescending(x => x.CheckInTime)
                    .ToListAsync();

                result.AddRange(logs.Select(x => new BusinessVehicleStatusDTO
                {
                    FleetWashLogId = x.FleetWashLogId,
                    BookingId = x.BookingId,
                    LicensePlate = x.FleetVehicle.LicensePlate,
                    DriverName = x.FleetVehicle.DriverName,
                    VehicleType = x.FleetVehicle.VehicleType.Name,
                    Status = x.Status!,
                    WashType = x.BookingId != null ? "Booking" : "WalkIn",
                    LaneName = x.Lane != null ? x.Lane.Name : null,
                    BranchName = null,
                    ScheduledTime = null,
                    CheckInTime = x.CheckInTime,
                    CompletedTime = x.CompletedTime,
                    WashCost = x.WashCost
                }));
            }

            return result.OrderByDescending(x => x.CheckInTime ?? x.ScheduledTime).ToList();
        }
    }        
}
