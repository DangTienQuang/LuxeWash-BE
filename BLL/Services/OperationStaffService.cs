using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using BLL.Helpers;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
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
        private readonly AutoWashPro.BLL.Services.Operations.ILaneDisplayPublisherService _laneDisplayPublisher;

        public OperationStaffService(AutoWashDbContext context, IWalletService walletService, IBookingMaterialUsageService bookingMaterialUsageService, global::BLL.Services.Interface.ILaneSchedulerService laneSchedulerService, global::AutoWashPro.BLL.Services.Interface.IOverloadSuggestionService overloadSuggestionService, AutoWashPro.BLL.Services.Operations.ILaneDisplayPublisherService laneDisplayPublisher)
        {
            _context = context;
            _walletService = walletService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
            _laneSchedulerService = laneSchedulerService;
            _overloadSuggestionService = overloadSuggestionService;
            _laneDisplayPublisher = laneDisplayPublisher;
        }

        public async Task<StaffLaneTaskDTO?> GetTodayLaneAssignmentAsync(int staffUserId, DateTime? date = null)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.ToVnTime().Date;

            var assignment = await _context.StaffLaneAssignments
                .Include(a => a.Lane)
                .Where(a => a.StaffId == staffUserId && a.AssignedDate.Date == targetDate)
                .OrderByDescending(a => a.AssignmentId)
                .FirstOrDefaultAsync();

            if (assignment == null)
            {
                return new StaffLaneTaskDTO
                {
                    LaneId = 0,
                    LaneName = "All Lanes",
                    AssignedDate = targetDate
                };
            }

            return new StaffLaneTaskDTO
            {
                LaneId = assignment.LaneId,
                LaneName = assignment.Lane.Name,
                AssignedDate = assignment.AssignedDate
            };
        }

        public async Task<bool> CheckInBookingAsync(int staffUserId, int bookingId)
        {
            var today = System.DateTime.UtcNow.ToVnTime().Date;
            var assignment = await _context.StaffLaneAssignments
                .Where(a => a.StaffId == staffUserId && a.AssignedDate == today)
                .OrderByDescending(a => a.AssignmentId)
                .FirstOrDefaultAsync();

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

            if (!await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, booking))
            {
                throw new BadRequestException("BOOKING_PAYMENT_REQUIRED");
            }

            if (booking.ProcessingLaneId == null)
            {
                var laneId = await _laneSchedulerService.AssignBestAvailableLaneAtomicAsync(bookingId);
                if (laneId > 0) booking.ProcessingLaneId = laneId;
            }

            booking.ProcessingStaffId = staffUserId;
            booking.Status = "CheckedIn";

            await _context.SaveChangesAsync();

            if (booking.ProcessingLaneId.HasValue)
            {
                // We need the license plate and lane name for the event
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == booking.VehicleId);
                var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == booking.ProcessingLaneId.Value);
                if (lane != null)
                {
                    await _laneDisplayPublisher.PublishEventAsync(new AutoWashPro.BLL.DTOs.Operations.LaneDisplayEventDTO
                    {
                        BranchId = booking.BranchId,
                        Type = "Reading", // Or CheckedIn depending on the stage, using Reading as an example
                        BookingId = booking.BookingId,
                        LicensePlate = vehicle?.LicensePlate,
                        LaneId = lane.LaneId,
                        LaneName = lane.Name
                    });
                }
            }

            // P0.4: Await the overload check — do NOT fire-and-forget with a scoped DbContext,
            // as the scope may be disposed before the async task completes (ObjectDisposedException).
            await _overloadSuggestionService.CheckAndTriggerOverloadAsync(booking.BranchId);

            return true;
        }

        public async Task<List<StaffBookingDTO>> GetAssignedBookingsAsync(int staffUserId, DateTime? date = null)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.ToVnTime().Date;

            var staffBranchId = await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == staffUserId)
                .Select(e => e.BranchId)
                .FirstOrDefaultAsync();

            var bookings = await _context.Bookings
                .Include(b => b.BookingDetails)
                .ThenInclude(d => d.Service)
                .Include(b => b.ActualVehicleType)
                .Include(b => b.User)
                .ThenInclude(u => u!.CustomerProfile)
                .ThenInclude(p => p!.Tier)
                .Include(b => b.ProcessingLane)
                .Where(b => b.BranchId == staffBranchId
                         && (b.Status == "CheckedIn" || b.Status == "Processing")
                         && (b.ScheduledTime.Date == targetDate || b.ProcessingStartTime.HasValue))
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
                        || t.TransactionType == "WalkInPayment"))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    BookingId = t.ReferenceBookingId!.Value,
                    t.Status,
                    t.PaymentMethod,
                    t.OrderCode
                })
                .ToListAsync();

            var latestPaymentByBooking = paymentTransactions
                .GroupBy(t => t.BookingId)
                .ToDictionary(g => g.Key, g => g.First());

            return bookings.Select(b =>
            {
                latestPaymentByBooking.TryGetValue(b.BookingId, out var tx);
                var paymentStatus = tx != null && string.Equals(tx.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                    ? "Completed"
                    : "Unpaid";
                return new StaffBookingDTO
                {
                    BookingId = b.BookingId,
                    LicensePlate = b.LicensePlate,
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                    VehicleTypeName = b.ActualVehicleType?.Name ?? "Unknown",
                    Status = b.Status,
                    PaymentStatus = paymentStatus,
                    PaymentMethod = tx?.PaymentMethod,
                    OrderCode = tx?.OrderCode,
                    FinalAmount = b.FinalAmount,
                    ProcessingStartTime = b.ProcessingStartTime.HasValue ? b.ProcessingStartTime.Value.ToVnTime() : (DateTime?)null,
                    CompletedTime = b.CompletedTime.HasValue ? b.CompletedTime.Value.ToVnTime() : (DateTime?)null,
                    ActualDurationMinutes = b.ActualDurationMinutes,
                    CustomerTierName = b.User?.CustomerProfile?.Tier?.TierName ?? "WalkIn / Standard",
                    CustomerTierPoints = b.User?.CustomerProfile?.Tier?.MinAccumulatedPoints ?? 0,
                    UserId = b.UserId,
                    ProcessingLaneId = b.ProcessingLaneId,
                    ProcessingLaneName = b.ProcessingLane?.Name
                };
            }).ToList();
        }

        public async Task<bool> UpdateBookingStatusAsync(int staffUserId, int bookingId, string newStatus)
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
                     
                 if (booking.FinalAmount > 0)
                 {
                     var hasCompletedPayment = await _context.Transactions
                         .AnyAsync(t => t.ReferenceBookingId == bookingId && t.Status == "Completed");
                     if (!hasCompletedPayment)
                     {
                         throw new BadRequestException("BOOKING_PAYMENT_REQUIRED");
                     }
                 }

                 if (booking.ProcessingLaneId == null)
                 {
                     throw new BadRequestException("Booking does not have an assigned lane; cannot start processing.");
                 }

                 booking.ProcessingStaffId = staffUserId;
                 booking.ProcessingStartTime = DateTime.UtcNow;
                 booking.CompletedTime = null;
                 booking.ActualDurationMinutes = null;
            }

            if (newStatus == "Completed")
            {
                if (booking.Status != "Processing" && booking.Status != "Completed")
                    throw new BadRequestException("Can only complete processing vehicles.");

                booking.ProcessingStaffId = staffUserId;
            }

            var isCompletingNow = newStatus == "Completed" && booking.Status != "Completed";
            booking.Status = newStatus;

            if (isCompletingNow)
            {
                booking.CompletedTime = DateTime.UtcNow;
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
                     userProfile.LastVisitDate = DateTime.UtcNow;
                 }
            }

            await _context.SaveChangesAsync();

            if (isCompletingNow && booking.ProcessingLaneId.HasValue)
            {
                var laneName = await _context.Lanes
                    .Where(l => l.LaneId == booking.ProcessingLaneId.Value)
                    .Select(l => l.Name)
                    .FirstOrDefaultAsync() ?? $"Lane {booking.ProcessingLaneId.Value}";

                // Fire cleared event because this booking finished.
                await _laneDisplayPublisher.PublishClearAsync(
                    booking.BranchId,
                    booking.ProcessingLaneId.Value,
                    laneName
                );

                await _laneSchedulerService.AssignNextVehicleInQueueAsync(booking.ProcessingLaneId.Value);
            }
            else if (newStatus == "Processing" && booking.ProcessingLaneId.HasValue)
            {
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == booking.VehicleId);
                var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == booking.ProcessingLaneId.Value);
                if (lane != null)
                {
                    await _laneDisplayPublisher.PublishEventAsync(new AutoWashPro.BLL.DTOs.Operations.LaneDisplayEventDTO
                    {
                        BranchId = booking.BranchId,
                        Type = "Processing",
                        BookingId = booking.BookingId,
                        LicensePlate = vehicle?.LicensePlate,
                        LaneId = lane.LaneId,
                        LaneName = lane.Name
                    });
                }
            }

            return true;
        }

        public async Task<bool> SwapShiftByPhoneAsync(int currentStaffId, SwapLaneByPhoneDTO dto)
        {
            var targetStaff = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.TargetPhoneNumber && u.Role == "Staff" && u.Status == "Active");

            if (targetStaff == null)
            {
                throw new BadRequestException("Employee with this phone number not found or unavailable.");
            }

            var targetDate = dto.Date?.Date ?? DateTime.UtcNow.ToVnTime().Date;

            var currentAssignment = await _context.StaffLaneAssignments
                .FirstOrDefaultAsync(a => a.StaffId == currentStaffId && a.AssignedDate.Date == targetDate);

            var targetAssignment = await _context.StaffLaneAssignments
                .FirstOrDefaultAsync(a => a.StaffId == targetStaff.UserId && a.AssignedDate.Date == targetDate);

            if (currentAssignment == null || targetAssignment == null)
            {
                throw new BadRequestException("One of the two employees does not have a shift assigned on this date to swap.");
            }

            // Swap lane IDs
            (currentAssignment.LaneId, targetAssignment.LaneId) = (targetAssignment.LaneId, currentAssignment.LaneId);

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
