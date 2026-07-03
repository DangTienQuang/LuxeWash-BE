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

        public OperationStaffService(AutoWashDbContext context, IWalletService walletService, IBookingMaterialUsageService bookingMaterialUsageService)
        {
            _context = context;
            _walletService = walletService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
        }

        public async Task<StaffLaneTaskDTO?> GetTodayLaneAssignmentAsync(int staffUserId, DateTime? date = null)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.ToVnTime().Date;

            var assignment = await _context.StaffLaneAssignments
                .Include(a => a.Lane)
                .Where(a => a.StaffId == staffUserId && a.AssignedDate.Date == targetDate)
                .OrderByDescending(a => a.AssignmentId)
                .FirstOrDefaultAsync();

            if (assignment == null) return null;

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

            if (assignment == null)
            {
                throw new BadRequestException("Bạn chưa được phân công vào làn nào trong hôm nay. Không thể check-in.");
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin đặt lịch.");
            }

            if (booking.Status != "Pending")
            {
                throw new BadRequestException("Chỉ có thể check-in cho xe đang ở trạng thái chờ (Pending).");
            }

            booking.ProcessingLaneId = assignment.LaneId;
            booking.ProcessingStaffId = staffUserId;
            booking.Status = "CheckedIn";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<StaffBookingDTO>> GetAssignedBookingsAsync(int staffUserId, DateTime? date = null)
        {
            var targetDate = date?.Date ?? DateTime.UtcNow.ToVnTime().Date;

            var assignment = await _context.StaffLaneAssignments
                .Where(a => a.StaffId == staffUserId && a.AssignedDate.Date == targetDate)
                .FirstOrDefaultAsync();

            if (assignment == null)
            {
                return new List<StaffBookingDTO>();
            }

            var bookings = await _context.Bookings
                .Include(b => b.BookingDetails)
                .ThenInclude(d => d.Service)
                .Include(b => b.ActualVehicleType)
                .Where(b => b.ProcessingLaneId == assignment.LaneId
                         && (b.ProcessingStaffId == staffUserId || b.ProcessingStaffId == null)
                         && (b.Status == "CheckedIn" || b.Status == "Processing"))
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
                    FinalAmount = b.FinalAmount
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
                 booking.ProcessingStaffId = staffUserId;
            }

            if (newStatus == "Completed")
            {
                if (booking.Status != "Processing" && booking.Status != "Completed")
                    throw new BadRequestException("Can only complete processing vehicles.");

                if (booking.ProcessingStaffId != staffUserId)
                    throw new BadRequestException("You are not assigned to this vehicle.");
            }

            var isCompletingNow = newStatus == "Completed" && booking.Status != "Completed";
            booking.Status = newStatus;

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

            return true;
        }

        public async Task<bool> SwapShiftByPhoneAsync(int currentStaffId, SwapLaneByPhoneDTO dto)
        {
            var targetStaff = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.TargetPhoneNumber && u.Role == "Staff" && u.Status == "Active");

            if (targetStaff == null)
            {
                throw new BadRequestException("Không tìm thấy nhân viên với số điện thoại này hoặc nhân viên không khả dụng.");
            }

            var targetDate = dto.Date?.Date ?? DateTime.UtcNow.ToVnTime().Date;

            var currentAssignment = await _context.StaffLaneAssignments
                .FirstOrDefaultAsync(a => a.StaffId == currentStaffId && a.AssignedDate.Date == targetDate);

            var targetAssignment = await _context.StaffLaneAssignments
                .FirstOrDefaultAsync(a => a.StaffId == targetStaff.UserId && a.AssignedDate.Date == targetDate);

            if (currentAssignment == null || targetAssignment == null)
            {
                throw new BadRequestException("Một trong hai nhân viên không có lịch phân công vào ngày này để đổi.");
            }

            // Swap lane IDs
            (currentAssignment.LaneId, targetAssignment.LaneId) = (targetAssignment.LaneId, currentAssignment.LaneId);

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
