using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using BLL.Helpers;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public class OperationStaffService : IOperationStaffService
    {
        private readonly AutoWashDbContext _context;
        private readonly IWalletService _walletService;
        private readonly IBookingMaterialUsageService _bookingMaterialUsageService;
        private readonly global::BLL.Services.Interface.ILaneSchedulerService _laneSchedulerService;
        private readonly global::AutoWashPro.BLL.Services.Interface.IOverloadSuggestionService _overloadSuggestionService;
        private readonly AutoWashPro.BLL.Services.Operations.ILaneAdmissionCoordinator _laneCoordinator;
        private readonly global::BLL.Services.Interface.IPhotoService _photoService;
        public OperationStaffService(AutoWashDbContext context, IWalletService walletService, IBookingMaterialUsageService bookingMaterialUsageService, global::BLL.Services.Interface.ILaneSchedulerService laneSchedulerService, global::AutoWashPro.BLL.Services.Interface.IOverloadSuggestionService overloadSuggestionService, AutoWashPro.BLL.Services.Operations.ILaneAdmissionCoordinator laneCoordinator, global::BLL.Services.Interface.IPhotoService photoService)
        {
            _context = context;
            _walletService = walletService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
            _laneSchedulerService = laneSchedulerService;
            _overloadSuggestionService = overloadSuggestionService;
            _laneCoordinator = laneCoordinator;
            _photoService = photoService;
        }
        public async Task<Operations.GateCheckInResult> CheckInBookingAsync(int staffUserId, int bookingId, Microsoft.AspNetCore.Http.IFormFile? checkInImage = null, bool allowOutsideScheduledTime = false)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null)
            {
                throw new NotFoundException("Booking information not found.");
            }
            if (booking.Status != "Pending")
            {
                throw new BadRequestException("Can only check in vehicles in Pending status.");
            }
            await BookingCheckInPolicy.ValidateAsync(
                _context,
                booking,
                allowOutsideScheduledTime);
            if (!await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, booking))
            {
                throw new BadRequestException("BOOKING_PAYMENT_REQUIRED");
            }
            if (booking.ProcessingLaneId != null)
            {
                var hasValidOccupancy = await _context.LaneOccupancies
                    .AnyAsync(o => o.BookingId == booking.BookingId);
                if (!hasValidOccupancy)
                {
                    booking.ProcessingLaneId = null;
                    booking.ProcessingStartTime = null;
                    booking.CompletedTime = null;
                    booking.ActualDurationMinutes = null;
                }
                else
                {
                    throw new BadRequestException("INVALID_STATE: Booking is in Pending status but already has a valid LaneOccupancy. Cannot check in again.");
                }
            }
            // Manual Staff check-in does not depend on the entrance camera.
            // Camera-driven check-in still sends and stores a frame when one is available.
            if (checkInImage != null && checkInImage.Length > 0)
            {
                booking.CheckInImageUrl = await _photoService.UploadImageAsync(checkInImage);
            }

            int? fleetWashLogId = null;
            var isBusinessBooking = booking.FleetVehicleId.HasValue &&
                (booking.BusinessProfileId.HasValue ||
                 string.Equals(booking.BookingType, "Business", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(booking.BookingType, "Fleet", StringComparison.OrdinalIgnoreCase));

            if (isBusinessBooking)
            {
                var fleetWashLog = await _context.FleetWashLogs
                    .FirstOrDefaultAsync(log =>
                        log.BookingId == booking.BookingId &&
                        log.Status != "Completed" &&
                        log.Status != "Cancelled");

                if (fleetWashLog == null)
                {
                    fleetWashLog = new FleetWashLog
                    {
                        FleetVehicleId = booking.FleetVehicleId!.Value,
                        BranchId = booking.BranchId,
                        BookingId = booking.BookingId,
                        CheckInTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                        Status = "CheckedIn",
                        WashCost = booking.FinalAmount,
                        CheckInImageUrl = booking.CheckInImageUrl
                    };
                    _context.FleetWashLogs.Add(fleetWashLog);
                    await _context.SaveChangesAsync();
                }

                fleetWashLogId = fleetWashLog.FleetWashLogId;
            }

            var checkInResult = await _laneCoordinator.CheckInAtEntryGateAsync(
                booking.LicensePlate ?? "UNKNOWN",
                booking.BranchId,
                bookingId: booking.BookingId,
                fleetWashLogId: fleetWashLogId);
            booking.ProcessingStaffId = staffUserId;
            await _context.SaveChangesAsync();
            await _overloadSuggestionService.CheckAndTriggerOverloadAsync(booking.BranchId);
            return checkInResult;
        }
        public async Task<List<StaffBookingDTO>> GetAssignedBookingsAsync(int staffUserId, DateTime? date = null)
        {
            var staffBranchId = await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == staffUserId)
                .Select(e => e.BranchId)
                .FirstOrDefaultAsync();
            var query = _context.Bookings
                .Include(b => b.BookingDetails)
                .ThenInclude(d => d.Service)
                .Include(b => b.ActualVehicleType)
                .Include(b => b.Vehicle).ThenInclude(v => v!.VehicleType)
                .Include(b => b.User)
                .ThenInclude(u => u!.CustomerProfile)
                .ThenInclude(p => p!.Tier)
                .Include(b => b.BusinessProfile)
                .Include(b => b.ProcessingLane)
                .Where(b => b.BranchId == staffBranchId
                         && (b.Status == "CheckedIn" || b.Status == "Processing"));
            if (date.HasValue)
            {
                query = query.Where(b => b.ScheduledTime.Date == date.Value.Date);
            }
            var bookings = await query
                .OrderByDescending(b => b.User != null && b.User.CustomerProfile != null && b.User.CustomerProfile.Tier != null
                                         ? b.User.CustomerProfile.Tier.MinAccumulatedPoints
                                         : -1)
                .ThenBy(b => b.ScheduledTime)
                .ToListAsync();
            if (bookings.Count == 0)
            {
                return new List<StaffBookingDTO>();
            }
            var bookingIds = bookings.Select(b => b.BookingId).Distinct().ToList();
            var paymentTransactions = await _context.Transactions
                .Where(t => t.ReferenceBookingId.HasValue
                    && bookingIds.Contains(t.ReferenceBookingId.Value)
                    && (t.TransactionType == "Payment"
                        || t.TransactionType == "BookingPayment"
                        || t.TransactionType == "WalkInPayment"
                        || t.TransactionType == "Refund"))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    BookingId = t.ReferenceBookingId!.Value,
                    t.Status,
                    t.PaymentMethod,
                    t.OrderCode,
                    t.TransactionType
                })
                .ToListAsync();
            var latestPaymentByBooking = paymentTransactions
                .GroupBy(t => t.BookingId)
                .ToDictionary(g => g.Key, g => new
                {
                    Tx = g.FirstOrDefault(x => x.TransactionType != "Refund"),
                    IsRefunded = g.Any(x => x.TransactionType == "Refund")
                });
            return bookings.Select(b =>
            {
                latestPaymentByBooking.TryGetValue(b.BookingId, out var paymentData);
                var isRefunded = paymentData?.IsRefunded ?? false;
                var tx = paymentData?.Tx;
                var isBusinessBooking = b.BusinessProfileId.HasValue ||
                    string.Equals(b.BookingType, "Business", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(b.BookingType, "Fleet", StringComparison.OrdinalIgnoreCase);
                var paymentStatus = isBusinessBooking
                    ? "Completed"
                    : isRefunded
                    ? "Refunded"
                    : tx == null
                    ? "Unpaid"
                    : tx.Status?.ToLowerInvariant() switch
                    {
                        "completed" => "Completed",
                        "pending"   => "Pending",
                        "expired"   => "Expired",
                        "failed"    => "Failed",
                        "cancelled" => "Failed",
                        _           => "Unpaid"
                    };
                var paymentNote = paymentStatus switch
                {
                    "Completed" => null,
                    "Refunded"  => "Payment has been refunded to customer",
                    "Pending"   => "Awaiting QR payment — please remind customer to complete scan",
                    "Expired"   => "Payment link expired — ask customer to generate a new QR or pay by another method",
                    "Failed"    => "Payment failed — please collect payment before processing",
                    _           => b.FinalAmount > 0 ? "Not yet paid — collect payment before processing" : null
                };
                return new StaffBookingDTO
                {
                    BookingId = b.BookingId,
                    LicensePlate = b.LicensePlate,
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                    VehicleTypeName = b.ActualVehicleType?.Name ?? b.Vehicle?.VehicleType?.Name ?? "Unknown",
                    Status = b.Status,
                    PaymentStatus = paymentStatus,
                    PaymentNote = paymentNote,
                    PaymentMethod = isBusinessBooking ? "Business account" : tx?.PaymentMethod,
                    OrderCode = tx?.OrderCode,
                    FinalAmount = b.FinalAmount,
                    ProcessingStartTime = b.ProcessingStartTime.HasValue ? b.ProcessingStartTime.Value : (DateTime?)null,
                    CompletedTime = b.CompletedTime.HasValue ? b.CompletedTime.Value : (DateTime?)null,
                    ActualDurationMinutes = b.ActualDurationMinutes,
                    CustomerTierName = isBusinessBooking
                        ? "Business account"
                        : b.User?.CustomerProfile?.Tier?.TierName ?? "WalkIn / Standard",
                    CustomerTierPoints = b.User?.CustomerProfile?.Tier?.MinAccumulatedPoints ?? 0,
                    UserId = b.UserId,
                    CustomerName = b.BusinessProfile?.CompanyName ?? b.User?.CustomerProfile?.FullName,
                    CustomerPhone = b.User?.PhoneNumber,
                    BookingType = b.BookingType,
                    ProcessingLaneId = b.ProcessingLaneId,
                    ProcessingLaneName = b.ProcessingLane?.Name
                };
            }).ToList();
        }
        public async Task<bool> UpdateBookingStatusAsync(int staffUserId, int bookingId, string newStatus, Microsoft.AspNetCore.Http.IFormFile? checkOutImage = null)
        {
            if (newStatus != "Processing" && newStatus != "Completed")
            {
                throw new BadRequestException("Invalid status update.");
            }
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) throw new NotFoundException("Booking not found.");
            if (newStatus == "Processing")
            {
                 if (booking.Status != "CheckedIn" && booking.Status != "Processing")
                     throw new BadRequestException("Can only start processing checked-in vehicles.");
                 // Use the shared payment policy here as well. Business/Fleet
                 // bookings are settled against the approved company account
                 // and intentionally do not have an individual payment
                 // transaction for every vehicle.
                 if (!await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, booking))
                 {
                     throw new BadRequestException("BOOKING_PAYMENT_REQUIRED");
                 }
                 if (booking.ProcessingLaneId == null)
                 {
                     throw new BadRequestException("Booking does not have an assigned lane; cannot start processing.");
                 }
                 var startedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                 booking.ProcessingStaffId = staffUserId;
                 booking.ProcessingStartTime = startedAt;
                 booking.CompletedTime = null;
                 booking.ActualDurationMinutes = null;

                 var linkedFleetWashLog = await _context.FleetWashLogs
                     .FirstOrDefaultAsync(log =>
                         log.BookingId == booking.BookingId &&
                         log.Status != "Completed" &&
                         log.Status != "Cancelled");
                 if (linkedFleetWashLog != null)
                 {
                     linkedFleetWashLog.Status = "Processing";
                     linkedFleetWashLog.LaneId = booking.ProcessingLaneId;
                 }

                 var occupancy = await _context.LaneOccupancies
                     .FirstOrDefaultAsync(item => item.BookingId == booking.BookingId);
                 if (occupancy != null)
                 {
                     occupancy.OccupiedAt = startedAt;
                 }
            }
            if (newStatus == "Completed")
            {
                if (booking.Status != "Processing" && booking.Status != "Completed")
                    throw new BadRequestException("Can only complete processing vehicles.");
                booking.ProcessingStaffId = staffUserId;
                if (checkOutImage != null && checkOutImage.Length > 0)
                {
                    booking.CheckOutImageUrl = await _photoService.UploadImageAsync(checkOutImage);
                }
            }
            var isCompletingNow = newStatus == "Completed" && booking.Status != "Completed";
            if (isCompletingNow && !await _context.LaneOccupancies.AnyAsync(o => o.BookingId == booking.BookingId))
            {
                throw new BadRequestException(
                    "The vehicle does not have an active lane occupancy; check-out was not completed.",
                    "LANE_OCCUPANCY_NOT_FOUND");
            }

            booking.Status = newStatus;
            if (isCompletingNow)
            {
                booking.CompletedTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                if (booking.ProcessingStartTime.HasValue)
                {
                    var duration = (int)Math.Round((booking.CompletedTime.Value - booking.ProcessingStartTime.Value).TotalMinutes);
                    booking.ActualDurationMinutes = duration < 1 ? 1 : duration;
                }
            }
            if (newStatus == "Completed")
            {
                await _bookingMaterialUsageService.ConsumeForCompletedBookingAsync(booking.BookingId, staffUserId);
            }
            if (isCompletingNow && booking.UserId > 0)
            {
                 var userProfile = await _context.CustomerProfiles
                        .Include(cp => cp.Tier)
                        .FirstOrDefaultAsync(cp => cp.UserId == booking.UserId);
                 if (userProfile?.Tier != null && booking.FinalAmount > 0)
                 {
                        int pointsEarned = (int)((booking.FinalAmount / PointConstants.VndPerEarnedPoint) * (decimal)userProfile.Tier.PointMultiplier);
                        if (pointsEarned > 0)
                        {
                            await _walletService.AwardCompletionPointsAsync(
                                booking.UserId.Value, pointsEarned, booking.BookingId);
                        }
                 }
                 if (userProfile != null)
                 {
                     userProfile.LastVisitDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                 }
            }
            if (isCompletingNow)
            {
                await _laneCoordinator.CompletePhysicalCheckoutAsync(booking.BookingId, staffUserId);
            }
            else if (newStatus == "Cancelled" || newStatus == "Delayed")
            {
                await _laneCoordinator.ReleaseLaneAsync(booking.BookingId, newStatus);
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<AutoWashPro.BLL.Services.Operations.LaneOccupancyDTO>> GetActiveLaneOccupanciesAsync(int staffUserId)
        {
            var staffBranchId = await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == staffUserId)
                .Select(e => e.BranchId)
                .FirstOrDefaultAsync();
            var occupancies = await _context.LaneOccupancies
                .AsNoTracking()
                .Include(o => o.Lane)
                .Include(o => o.Booking)
                .Include(o => o.FleetWashLog)
                    .ThenInclude(f => f!.FleetVehicle)
                        .ThenInclude(v => v.BusinessProfile)
                .Include(o => o.FleetWashLog)
                    .ThenInclude(f => f!.FleetVehicle)
                        .ThenInclude(v => v.VehicleType)
                .Include(o => o.FleetWashLog)
                    .ThenInclude(f => f!.Booking)
                        .ThenInclude(b => b!.BookingDetails)
                            .ThenInclude(d => d.Service)
                .Where(o =>
                    o.BranchId == staffBranchId &&
                    (!o.BookingId.HasValue ||
                        (o.Booking != null &&
                         (o.Booking.Status == "CheckedIn" || o.Booking.Status == "Processing"))) &&
                    (!o.FleetWashLogId.HasValue ||
                        (o.FleetWashLog != null &&
                         (o.FleetWashLog.Status == "CheckedIn" ||
                          o.FleetWashLog.Status == "Assigned" ||
                          o.FleetWashLog.Status == "Processing"))))
                .ToListAsync();

            return occupancies.Select(o =>
            {
                var fleetLog = o.FleetWashLog;
                var fleetBooking = fleetLog?.Booking;
                return new AutoWashPro.BLL.Services.Operations.LaneOccupancyDTO
                {
                    LaneId = o.LaneId,
                    LicensePlate = o.LicensePlate,
                    BookingId = o.BookingId,
                    FleetWashLogId = o.FleetWashLogId,
                    Status = fleetLog?.Status ?? fleetBooking?.Status ?? o.Booking?.Status ?? "CheckedIn",
                    // Database timestamps are UTC. Staff APIs expose wall-clock Vietnam time,
                    // so convert here as well to avoid a seven-hour live-duration offset.
                    OccupiedAt = o.OccupiedAt,
                    LaneName = o.Lane?.Name ?? "",
                    CustomerName = fleetLog?.FleetVehicle?.BusinessProfile?.CompanyName,
                    DriverName = fleetLog?.FleetVehicle?.DriverName,
                    VehicleTypeName = fleetLog?.FleetVehicle?.VehicleType?.Name,
                    ServiceNames = fleetBooking?.BookingDetails
                        .Select(d => d.Service.ServiceName)
                        .ToList() ?? new List<string>(),
                    FinalAmount = fleetLog?.WashCost ?? 0,
                    BookingType = fleetLog != null ? "Fleet" : null
                };
            }).ToList();
        }
    }
}
