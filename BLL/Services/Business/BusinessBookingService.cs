#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Helpers;
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
        private readonly AutoWashPro.BLL.Services.Operations.ILaneAdmissionCoordinator _laneCoordinator;
        private readonly IBookingMaterialUsageService _bookingMaterialUsageService;
        private readonly ILaneSchedulerService _laneSchedulerService;
        public BusinessBookingService(
            AutoWashDbContext context,
            AutoWashPro.BLL.Services.Operations.ILaneAdmissionCoordinator laneCoordinator,
            IBookingMaterialUsageService bookingMaterialUsageService,
            ILaneSchedulerService laneSchedulerService)
        {
            _context = context;
            _laneCoordinator = laneCoordinator;
            _bookingMaterialUsageService = bookingMaterialUsageService;
            _laneSchedulerService = laneSchedulerService;
        }
        public async Task<List<DTOs.Business.TimeSlotResponseDTO>> GetAvailableSlotsForBusinessAsync(int businessUserId, CheckBusinessSlotsRequestDTO request)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x =>
                    x.UserId == businessUserId &&
                    x.ApprovalStatus == "Approved");
            if (business == null)
                throw new NotFoundException("Business profile not found or not approved.");
            var requestedVehicles = request.Vehicles.Count > 0
                ? request.Vehicles
                : Enumerable.Range(0, Math.Max(1, request.VehicleCount ?? 1))
                    .Select(_ => new VehicleBookingItemDTO
                    {
                        FleetVehicleId = request.FleetVehicleId,
                        ServiceIds = request.ServiceIds
                    })
                    .ToList();
            if (requestedVehicles.Any(x => x.FleetVehicleId <= 0))
                throw new BadRequestException("Please select at least one vehicle.");

            var vehicleIds = requestedVehicles.Select(x => x.FleetVehicleId).Distinct().ToList();
            var fleetVehicles = await _context.FleetVehicles
                .Include(x => x.VehicleType)
                .Where(x =>
                    vehicleIds.Contains(x.FleetVehicleId) &&
                    x.BusinessProfileId == business.BusinessProfileId &&
                    x.Status == "Active")
                .ToListAsync();
            if (fleetVehicles.Count != vehicleIds.Count)
                throw new NotFoundException("One or more vehicles were not found or are not activated.");
            TimeZoneInfo vnTimeZone;
            try { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            DateTime todayInVN = TimeZoneInfo.ConvertTimeFromUtc(AutoWashPro.DAL.Helpers.TimeHelper.VnNow, vnTimeZone).Date;
            TimeSpan currentTimeInVN = TimeZoneInfo.ConvertTimeFromUtc(AutoWashPro.DAL.Helpers.TimeHelper.VnNow, vnTimeZone).TimeOfDay;
            if (request.TargetDate.Date < todayInVN)
                throw new BadRequestException("Cannot book for a date in the past.");
            var simRequests = new List<VehicleScheduleRequest>();
            foreach (var item in requestedVehicles)
            {
                var vehicle = fleetVehicles.First(x => x.FleetVehicleId == item.FleetVehicleId);
                if (item.ServiceIds.Count == 0)
                    throw new BadRequestException($"Vehicle {vehicle.LicensePlate} must have at least one service.");

                var distinctServiceIds = item.ServiceIds.Distinct().ToList();
                var servicePrices = await _context.ServicePrices
                    .Where(x =>
                        x.BranchId == request.BranchId &&
                        x.VehicleTypeId == vehicle.VehicleTypeId &&
                        distinctServiceIds.Contains(x.ServiceId))
                    .ToListAsync();
                if (servicePrices.Count != distinctServiceIds.Count)
                    throw new BadRequestException(
                        $"One or more services have not been priced for vehicle {vehicle.LicensePlate}.");

                var capacityWeight = servicePrices
                    .Select(x => x.CapacityWeight > 0 ? x.CapacityWeight : vehicle.VehicleType.BaseWeight)
                    .DefaultIfEmpty(vehicle.VehicleType.BaseWeight)
                    .Max();
                simRequests.Add(new VehicleScheduleRequest
                {
                    FleetVehicleId = vehicle.FleetVehicleId,
                    VehicleType = vehicle.VehicleType,
                    ServicePrices = servicePrices,
                    CapacityWeight = capacityWeight
                });
            }
            var allSlots = await _context.TimeSlots
                .Where(s => s.BranchId == request.BranchId)
                .OrderBy(s => s.StartTime)
                .ToListAsync();
            var slotOrder = allSlots
                .Select((timeSlot, index) => new { timeSlot.SlotId, Index = index })
                .ToDictionary(x => x.SlotId, x => x.Index);
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
                if (request.TargetDate.Date == todayInVN && slot.StartTime < currentTimeInVN)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = "Past time";
                    response.Add(slotDto);
                    continue;
                }
                DateTime slotStart = request.TargetDate.Date.Add(slot.StartTime);
                var simResult = await _laneSchedulerService.ScheduleFleetAcrossSlotsAsync(
                    request.BranchId,
                    request.TargetDate,
                    slot.SlotId,
                    simRequests);
                if (!simResult.Success)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = simResult.ErrorMessage!;
                }
                else
                {
                    var lastEnd = simResult.Assignments.Max(a => a.EstimatedEnd);
                    slotDto.EstimatedLastEndMinutesIntoSlot =
                        (int)(lastEnd - slotStart).TotalMinutes;
                    slotDto.OverflowSlotCount = simResult.Assignments
                        .Select(a => slotOrder[a.AssignedSlotId] - slotOrder[slot.SlotId])
                        .DefaultIfEmpty(0)
                        .Max();
                    slotDto.VehicleProjections = simResult.Assignments
                        .Select(a => new VehicleSlotProjectionDTO
                        {
                            FleetVehicleId = a.FleetVehicleId,
                            SlotId = a.AssignedSlotId,
                            EstimatedStart = a.EstimatedStart,
                            EstimatedEnd = a.EstimatedEnd
                        })
                        .ToList();
                }
                response.Add(slotDto);
            }
            return response;
        }
        public async Task<MultiVehicleBookingResponseDTO> CreateBusinessBookingAsync(int businessUserId, CreateBusinessBookingDTO dto)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x =>
                    x.UserId == businessUserId &&
                    x.ApprovalStatus == "Approved");
            if (business == null)
                throw new NotFoundException("Business profile not found.");
            if (dto.Vehicles.Count == 0)
                throw new BadRequestException("Please select at least one vehicle.");
            var vehicleIds = dto.Vehicles.Select(v => v.FleetVehicleId).Distinct().ToList();
            if (vehicleIds.Count != dto.Vehicles.Count)
                throw new BadRequestException("A vehicle cannot be added to the same booking more than once.");
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
            var scheduleRequests = new List<VehicleScheduleRequest>();
            var capacityWeights = new Dictionary<int, int>();
            foreach (var item in dto.Vehicles)
            {
                var vehicle = fleetVehicles.First(v => v.FleetVehicleId == item.FleetVehicleId);
                if (!item.ServiceIds.Any())
                    throw new BadRequestException(
                        $"Vehicle {vehicle.LicensePlate} must have at least one service.");
                var distinctServiceIds = item.ServiceIds.Distinct().ToList();
                var vehicleServicePrices = await _context.ServicePrices
                    .Where(sp =>
                        sp.BranchId == dto.BranchId &&
                        sp.VehicleTypeId == vehicle.VehicleTypeId &&
                        distinctServiceIds.Contains(sp.ServiceId))
                    .ToListAsync();
                if (vehicleServicePrices.Count != distinctServiceIds.Count)
                    throw new BadRequestException(
                        $"One or more services have not been priced for the vehicle " +
                        $"{vehicle.LicensePlate} ({vehicle.VehicleType.Name}).");
                capacityWeights[vehicle.FleetVehicleId] = vehicleServicePrices
                    .Select(x => x.CapacityWeight > 0 ? x.CapacityWeight : vehicle.VehicleType.BaseWeight)
                    .DefaultIfEmpty(vehicle.VehicleType.BaseWeight)
                    .Max();
                scheduleRequests.Add(new VehicleScheduleRequest
                {
                    FleetVehicleId = vehicle.FleetVehicleId,
                    VehicleType = vehicle.VehicleType,
                    ServicePrices = vehicleServicePrices,
                    CapacityWeight = capacityWeights[vehicle.FleetVehicleId]
                });
            }
            var scheduleResult = await _laneSchedulerService.ScheduleFleetAcrossSlotsAsync(
                dto.BranchId,
                dto.ScheduledTime,
                slot.SlotId,
                scheduleRequests);
            if (!scheduleResult.Success)
                throw new BadRequestException(scheduleResult.ErrorMessage!);
            var laneIds = scheduleResult.Assignments.Select(a => a.LaneId).Distinct().ToList();
            var laneNames = await _context.Lanes
                .Where(x => laneIds.Contains(x.LaneId))
                .ToDictionaryAsync(x => x.LaneId, x => x.Name);
            var vehicleSummaries = new List<VehicleBookingSummaryDTO>();
            decimal totalAmount = scheduleRequests.Sum(request => request.ServicePrices.Sum(price => price.Price));
            var billingPeriodStart = new DateTime(scheduledTime.Year, scheduledTime.Month, 1);
            var billingPeriodEnd = billingPeriodStart.AddMonths(1);
            var committedAmount = await _context.Bookings
                .Where(booking =>
                    booking.BusinessProfileId == business.BusinessProfileId &&
                    booking.ScheduledTime >= billingPeriodStart &&
                    booking.ScheduledTime < billingPeriodEnd &&
                    booking.Status != "Cancelled" &&
                    booking.Status != "NoShow")
                .SumAsync(booking => (decimal?)booking.FinalAmount) ?? 0;
            if (business.MonthlyCreditLimit > 0 &&
                committedAmount + totalAmount > business.MonthlyCreditLimit)
            {
                throw new BadRequestException(
                    "Doanh nghiệp không còn đủ hạn mức tín dụng để tạo đặt lịch này.",
                    "BUSINESS_CREDIT_LIMIT_EXCEEDED");
            }

            var assignedSlotIds = scheduleResult.Assignments
                .Select(x => x.AssignedSlotId)
                .Distinct()
                .ToList();
            var assignedSlots = await _context.TimeSlots
                .Where(x => assignedSlotIds.Contains(x.SlotId))
                .ToDictionaryAsync(x => x.SlotId);
            var requiredWeightBySlot = scheduleResult.Assignments
                .GroupBy(x => x.AssignedSlotId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(x => capacityWeights[x.FleetVehicleId]));
            var createdBookingsByPlate = new Dictionary<string, Booking>();
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                foreach (var (assignedSlotId, requiredWeight) in requiredWeightBySlot)
                {
                    var dailyCapacity = await _context.DailySlotCapacities
                        .FirstOrDefaultAsync(x =>
                            x.BranchId == dto.BranchId &&
                            x.SlotId == assignedSlotId &&
                            x.Date == scheduledTime.Date);
                    if (dailyCapacity == null)
                    {
                        dailyCapacity = new DailySlotCapacity
                        {
                            BranchId = dto.BranchId,
                            SlotId = assignedSlotId,
                            Date = scheduledTime.Date,
                            BookedWeight = 0
                        };
                        _context.DailySlotCapacities.Add(dailyCapacity);
                    }
                    if (dailyCapacity.BookedWeight + requiredWeight > assignedSlots[assignedSlotId].MaxCapacity)
                        throw new BadRequestException("BUSINESS_SLOT_CAPACITY_EXCEEDED");
                    dailyCapacity.BookedWeight += requiredWeight;
                }

                foreach (var item in dto.Vehicles)
                {
                    var vehicle = fleetVehicles.First(v => v.FleetVehicleId == item.FleetVehicleId);
                    var request = scheduleRequests.First(r => r.FleetVehicleId == item.FleetVehicleId);
                    var assignment = scheduleResult.Assignments.First(a => a.FleetVehicleId == item.FleetVehicleId);
                    decimal vehicleTotal = request.ServicePrices.Sum(sp => sp.Price);
                    var booking = new Booking
                    {
                        UserId = business.UserId,
                        BusinessProfileId = business.BusinessProfileId,
                        FleetVehicleId = vehicle.FleetVehicleId,
                        BookingType = "Business",
                        BranchId = dto.BranchId,
                        ScheduledTime = assignment.EstimatedStart,
                        LicensePlate = vehicle.LicensePlate,
                        Status = "Pending",
                        OriginalPrice = vehicleTotal,
                        FinalAmount = vehicleTotal,
                        CapacityWeight = capacityWeights[vehicle.FleetVehicleId],
                        ActualVehicleTypeId = vehicle.VehicleTypeId,
                        FallbackQrCode = Guid.NewGuid().ToString("N")[..8].ToUpper()
                    };
                    _context.Bookings.Add(booking);
                    createdBookingsByPlate[vehicle.LicensePlate] = booking;
                    foreach (var sp in request.ServicePrices)
                    {
                        _context.BookingDetails.Add(new BookingDetail
                        {
                            Booking = booking,
                            ServiceId = sp.ServiceId,
                            Price = sp.Price
                        });
                    }
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
                foreach (var summary in vehicleSummaries)
                {
                    if (createdBookingsByPlate.TryGetValue(summary.LicensePlate, out var booking))
                        summary.BookingId = booking.BookingId;
                }
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
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
            if (booking.ScheduledTime <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddHours(24))
                throw new BadRequestException(
                    "Cannot reschedule within 24 hours of the appointment time. " +
                    "Please contact the branch for support.");
            var newSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(x =>
                    x.SlotId == dto.NewSlotId &&
                    x.BranchId == booking.BranchId);
            if (newSlot == null)
                throw new NotFoundException("New time slot not found.");
            DateTime newScheduledTime = dto.NewScheduledDate.Date.Add(newSlot.StartTime);
            if (newScheduledTime <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddHours(24))
                throw new BadRequestException(
                    "The new time slot must be at least 24 hours from the current time.");
            bool isSameSlot =
                booking.ScheduledTime.Date == dto.NewScheduledDate.Date &&
                booking.ScheduledTime.TimeOfDay >= newSlot.StartTime &&
                booking.ScheduledTime.TimeOfDay < newSlot.EndTime;
            if (isSameSlot)
                throw new BadRequestException("The new time slot is identical to the current time slot.");
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
            var scheduleRequest = new List<VehicleScheduleRequest>
    {
        new VehicleScheduleRequest
        {
            FleetVehicleId = booking.FleetVehicleId!.Value,
            VehicleType    = booking.FleetVehicle!.VehicleType,
            ServicePrices  = servicePrices,
            CapacityWeight = booking.CapacityWeight
        }
    };
            var scheduleResult = await _laneSchedulerService.ScheduleFleetAcrossSlotsAsync(
                booking.BranchId,
                dto.NewScheduledDate,
                newSlot.SlotId,
                scheduleRequest,
                excludedBookingId: booking.BookingId);
            if (!scheduleResult.Success)
                throw new BadRequestException(scheduleResult.ErrorMessage!);
            var newAssignment = scheduleResult.Assignments.First();
            var newLane = await _context.Lanes
                .FirstOrDefaultAsync(x => x.LaneId == newAssignment.LaneId);
            var assignedSlot = await _context.TimeSlots
                .FirstAsync(x => x.SlotId == newAssignment.AssignedSlotId);
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var oldSlot = await _context.TimeSlots
                    .FirstOrDefaultAsync(x =>
                        x.BranchId == booking.BranchId &&
                        x.StartTime <= booking.ScheduledTime.TimeOfDay &&
                        x.EndTime > booking.ScheduledTime.TimeOfDay);
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
                var newDailyCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(x =>
                        x.BranchId == booking.BranchId &&
                        x.SlotId == assignedSlot.SlotId &&
                        x.Date == dto.NewScheduledDate.Date);
                if (newDailyCapacity == null)
                {
                    newDailyCapacity = new DailySlotCapacity
                    {
                        BranchId = booking.BranchId,
                        SlotId = assignedSlot.SlotId,
                        Date = dto.NewScheduledDate.Date,
                        BookedWeight = 0
                    };
                    _context.DailySlotCapacities.Add(newDailyCapacity);
                }
                if (newDailyCapacity.BookedWeight + booking.CapacityWeight > assignedSlot.MaxCapacity)
                    throw new BadRequestException("The new time slot is fully booked.");
                newDailyCapacity.BookedWeight += booking.CapacityWeight;
                DateTime oldScheduledTime = booking.ScheduledTime;
                booking.ScheduledTime = newAssignment.EstimatedStart;
                booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return new RescheduleBusinessResponseDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate,
                    OldScheduledTime = oldScheduledTime,
                    NewScheduledTime = newAssignment.EstimatedStart,
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
                    x.StartTime <= booking.ScheduledTime.TimeOfDay &&
                    x.EndTime > booking.ScheduledTime.TimeOfDay);
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
            booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
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
                CheckInTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                WashCost = booking.FinalAmount,
                Status = "CheckedIn"
            };
            _context.FleetWashLogs.Add(washLog);
            await _context.SaveChangesAsync();
            var admission = await _laneCoordinator.CheckInAtEntryGateAsync(
                booking.LicensePlate,
                booking.BranchId,
                bookingId: booking.BookingId,
                fleetWashLogId: washLog.FleetWashLogId);
            booking.Status = "CheckedIn";
            booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
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
                     x.Status == "Assigned" ||
                     x.Status == "Processing"));
            if (existingLog != null)
            {
                throw new BadRequestException("This vehicle is currently undergoing wash processing.");
            }
            // A Staff walk-in for a Fleet vehicle must consume that vehicle's next
            // pending booking when one exists. Otherwise the wash log completes while
            // the booking remains Pending and Business can incorrectly reschedule it.
            var pendingBooking = await _context.Bookings
                .Where(x =>
                    x.FleetVehicleId == vehicle.FleetVehicleId &&
                    x.BranchId == dto.BranchId &&
                    x.BookingType == "Business" &&
                    x.Status == "Pending")
                .OrderBy(x => x.ScheduledTime)
                .ThenBy(x => x.BookingId)
                .FirstOrDefaultAsync();

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var washLog = new FleetWashLog
            {
                FleetVehicleId = vehicle.FleetVehicleId,
                BranchId = dto.BranchId,
                BookingId = pendingBooking?.BookingId,
                CheckInTime = now,
                Status = "CheckedIn",
                WashCost = pendingBooking?.FinalAmount ?? 0
            };
            if (pendingBooking != null)
            {
                pendingBooking.Status = "CheckedIn";
                pendingBooking.UpdatedAt = now;
            }
            _context.FleetWashLogs.Add(washLog);
            await _context.SaveChangesAsync();
            var admission = await _laneCoordinator.CheckInAtEntryGateAsync(
                vehicle.LicensePlate,
                dto.BranchId,
                bookingId: pendingBooking?.BookingId,
                fleetWashLogId: washLog.FleetWashLogId);
            return new FleetCheckInResponseDTO
            {
                FleetWashLogId = washLog.FleetWashLogId,
                BookingId = pendingBooking?.BookingId,
                FleetVehicleId = vehicle.FleetVehicleId,
                LicensePlate = vehicle.LicensePlate,
                DriverName = vehicle.DriverName,
                CheckInTime = washLog.CheckInTime,
                Status = washLog.Status!,
                IsWaiting = admission.IsWaiting,
                LaneId = admission.LaneId,
                LaneName = admission.LaneName
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
            washLog.CompletedTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var vehicle = await _context.FleetVehicles.FindAsync(washLog.FleetVehicleId);
            if (vehicle != null)
            {
                await _laneCoordinator.CheckOutAtExitGateAsync(vehicle.LicensePlate ?? "", washLog.BranchId);
            }
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
            var startedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            washLog.Status = "Processing";
            var occupancy = await _context.LaneOccupancies
                .FirstOrDefaultAsync(x => x.FleetWashLogId == washLogId);
            if (occupancy != null)
            {
                // OccupiedAt is also the live Fleet wash timer source on the Staff UI.
                occupancy.OccupiedAt = startedAt;
            }
            if (washLog.Booking != null)
            {
                washLog.Booking.ProcessingStaffId = staffUserId;
                washLog.Booking.ProcessingLaneId = washLog.LaneId ?? dto.LaneId;
                washLog.Booking.ProcessingStartTime = startedAt;
                washLog.Booking.CompletedTime = null;
                washLog.Booking.ActualDurationMinutes = null;
                washLog.Booking.Status = "Processing";
                washLog.Booking.UpdatedAt = startedAt;
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
                .Include(x => x.FleetVehicle)
                .Include(x => x.Booking)
                    .ThenInclude(x => x.BookingDetails)
                .FirstOrDefaultAsync(x =>
                    x.FleetWashLogId == washLogId &&
                    x.Status != "Completed");
            if (washLog == null)
            {
                throw new NotFoundException("Car wash log not found.");
            }
            var hasActiveOccupancy = await _context.LaneOccupancies
                .AnyAsync(x => x.FleetWashLogId == washLogId);
            var isActiveInWashBay = washLog.Status == "Processing" ||
                (washLog.Status == "Assigned" && hasActiveOccupancy);
            if (!isActiveInWashBay)
            {
                throw new BadRequestException(
                    "Can only check out Fleet vehicles that are currently occupying a wash lane.");
            }
            // Manual Fleet completion uses the same authoritative lane-release path as
            // camera checkout. This completes both the wash log and linked booking,
            // frees the lane, and admits the next waiting vehicle when possible.
            await _laneCoordinator.CheckOutAtExitGateAsync(
                washLog.FleetVehicle.LicensePlate,
                washLog.BranchId);

            // A legacy log may no longer have an occupancy. It must still be possible
            // for Staff to close it manually instead of leaving it stuck in Processing.
            if (washLog.Status != "Completed")
            {
                washLog.Status = "Completed";
                washLog.CompletedTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                washLog.LaneId = null;
                if (washLog.Booking != null)
                {
                    washLog.Booking.Status = "Completed";
                    washLog.Booking.CompletedTime = washLog.CompletedTime;
                    washLog.Booking.ProcessingLaneId = null;
                    washLog.Booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                }
            }
            if (washLog.BookingId.HasValue)
            {
                await _bookingMaterialUsageService.ConsumeForCompletedBookingAsync(washLog.BookingId.Value);
            }
            await _context.SaveChangesAsync();
            return new FleetCheckoutResponseDTO
            {
                FleetWashLogId = washLog.FleetWashLogId,
                TotalAmount = washLog.WashCost,
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
            var history = await query
                .OrderByDescending(x => x.CheckInTime)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new
                {
                    x.FleetWashLogId,
                    x.FleetVehicleId,
                    x.FleetVehicle.LicensePlate,
                    VehicleType = x.FleetVehicle.VehicleType.Name,
                    BranchName =
                        x.Booking != null
                            ? x.Booking.Branch.Name
                            : "Walk-In",
                    x.CheckInTime,
                    ProcessingStartTime = x.Booking != null ? x.Booking.ProcessingStartTime : null,
                    x.CompletedTime,
                    ActualDurationMinutes = x.Booking != null ? x.Booking.ActualDurationMinutes : null,
                    Status = x.Status!,
                    x.WashCost,
                    x.BookingId,
                    WashType =
                        x.BookingId != null
                            ? "Booking"
                            : "WalkIn"
                })
                .ToListAsync();

            return history.Select(x => new FleetWashHistoryDTO
            {
                FleetWashLogId = x.FleetWashLogId,
                LicensePlate = x.LicensePlate,
                VehicleType = x.VehicleType,
                BranchName = x.BranchName,
                CheckInTime = x.CheckInTime,
                ProcessingStartTime = x.ProcessingStartTime.HasValue
                    ? x.ProcessingStartTime.Value
                    : null,
                CompletedTime = x.CompletedTime.HasValue
                    ? x.CompletedTime.Value
                    : null,
                ActualDurationMinutes = x.ActualDurationMinutes,
                Status = x.Status,
                WashCost = x.WashCost,
                BookingId = x.BookingId,
                WashType = x.WashType
            }).ToList();
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
                    LicensePlate = x.Booking != null ? x.Booking.LicensePlate : null,
                    InvoiceType = x.InvoiceType
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
                LicensePlate = invoice.Booking != null ? invoice.Booking.LicensePlate : null,
                InvoiceType = invoice.InvoiceType,
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
            if (washLog.LaneId != null)
            {
                throw new BadRequestException("Vehicle already has a lane assigned.");
            }
            var staff = await _context.Users.FirstOrDefaultAsync(x => x.UserId == dto.StaffUserId);
            if (staff == null)
            {
                throw new NotFoundException("Employee not found.");
            }
            var vehicle = await _context.FleetVehicles.FirstOrDefaultAsync(v => v.FleetVehicleId == washLog.FleetVehicleId);
            var checkInResult = await _laneCoordinator.CheckInAtEntryGateAsync(
                vehicle?.LicensePlate ?? "UNKNOWN",
                washLog.BranchId,
                bookingId: washLog.BookingId,
                fleetWashLogId: washLog.FleetWashLogId,
                forcedLaneId: dto.LaneId);
            if (checkInResult.IsWaiting || !checkInResult.LaneId.HasValue)
            {
                throw new BadRequestException("LANE_UNAVAILABLE");
            }
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
                BranchId = x.BranchId,
                LaneId = x.LaneId,
                LaneName = x.Lane?.Name,
                BranchName = null,
                ScheduledTime = null,
                CheckInTime = x.CheckInTime,
                CompletedTime = x.CompletedTime,
                WashCost = x.WashCost
            }));
            return result
                .OrderBy(x => x.CheckInTime)
                .ToList();
        }
        public async Task<List<BusinessVehicleStatusDTO>> GetVehiclesByStatusAsync(int businessUserId, string? status)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);
            if (business == null)
                throw new NotFoundException("Business profile not found.");
            var result = new List<BusinessVehicleStatusDTO>();
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
                    BranchId = x.BranchId,
                    LaneId = x.ProcessingLaneId,
                    LaneName = null,
                    BranchName = x.Branch.Name,
                    ScheduledTime = x.ScheduledTime,
                    CheckInTime = null,
                    CompletedTime = null,
                    WashCost = x.FinalAmount
                }));
            }
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
                    BranchId = x.BranchId,
                    LaneId = x.LaneId,
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
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
