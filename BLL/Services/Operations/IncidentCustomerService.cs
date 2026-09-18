using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class IncidentCustomerService : IIncidentCustomerService
    {
        private readonly AutoWashDbContext _context;
        private readonly IBookingService _bookingService;
        private readonly IWalletService _walletService;
        private readonly IIncidentCapacityService _capacityService;
        private readonly IEmailService _emailService;

        public IncidentCustomerService(
            AutoWashDbContext context, 
            IBookingService bookingService,
            IWalletService walletService,
            IIncidentCapacityService capacityService,
            IEmailService emailService)
        {
            _context = context;
            _bookingService = bookingService;
            _walletService = walletService;
            _capacityService = capacityService;
            _emailService = emailService;
        }

        public async Task<IncidentDecisionResponseDTO> ProcessIncidentDecisionAsync(int userId, int bookingId, IncidentDecisionRequestDTO request)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Incident)
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .FirstOrDefaultAsync(c => c.Id == request.CaseId && c.BookingId == bookingId && c.UserId == userId);

            if (caseRecord == null)
                throw new NotFoundException("Affected booking record not found.");
            
            if (caseRecord.Incident != null && caseRecord.Incident.Version != request.ExpectedVersion)
                throw new ConflictException("INCIDENT_VERSION_CHANGED");

            if (caseRecord.Status == "AwaitingCustomer" && now > caseRecord.ResponseDeadlineAtVn)
                throw new BadRequestException("Response deadline has expired.");

            if (caseRecord.Status != "AwaitingCustomer")
            {
                if (caseRecord.Decision == request.Decision)
                {

                    if (request.Decision == "Transfer" && (caseRecord.TargetBranchId != request.TargetBranchId || caseRecord.TargetSlotId != request.TargetSlotId))
                    {
                        throw new ConflictException("DECISION_ALREADY_FINAL");
                    }
                    

                    UserVoucher? existingVoucher = null;
                    if (caseRecord.CompensationUserVoucherId.HasValue)
                    {
                        existingVoucher = await _context.UserVouchers.FindAsync(caseRecord.CompensationUserVoucherId.Value);
                    }
                    
                    return new IncidentDecisionResponseDTO
                    {
                        Decision = caseRecord.Decision,
                        Booking = caseRecord.Booking,
                        CaseStatus = caseRecord.Status,
                        Refund = caseRecord.Decision == "Cancel" ? new RefundPreviewDTO
                        {
                            Amount = caseRecord.Booking.FinalAmount,
                            Destination = "Wallet",
                            PointsRestored = caseRecord.Booking.PointsUsed,
                            OriginalVoucherRestored = caseRecord.Booking.AppliedVoucherId.HasValue
                        } : null,
                        CompensationVoucher = existingVoucher != null ? new CompensationVoucherDTO
                        {
                            VoucherId = existingVoucher.VoucherId,
                            DiscountPercent = 20,
                            ExpiresAt = existingVoucher.ExpiryDate.ToString("yyyy-MM-dd")
                        } : null
                    };
                }
                
                throw new ConflictException("DECISION_ALREADY_FINAL");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            if (request.Decision == "Cancel")
            {
                await HandleCancelDecisionAsync(caseRecord, now);
            }
            else if (request.Decision == "Transfer")
            {
                if (request.TargetBranchId == null || request.TargetSlotId == null)
                    throw new BadRequestException("Target branch and slot must be provided for Transfer.");

                await HandleTransferDecisionAsync(userId, caseRecord, request.TargetBranchId.Value, request.TargetSlotId.Value, now);
            }
            else if (request.Decision == "Keep")
            {
                if (caseRecord.Incident == null || caseRecord.Incident.Status != "Resolved")
                    throw new BadRequestException("You can only keep the original booking if the incident has been resolved.");


                caseRecord.Status = "Kept";
                caseRecord.Decision = "Keep";
                caseRecord.DecidedAtVn = now;
            }
            else
            {
                throw new BadRequestException("Invalid decision.");
            }


            UserVoucher? voucher = null;
            if (request.Decision != "Keep")
            {
                voucher = await IssueCompensationVoucherAsync(userId, caseRecord, now);
            }
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new IncidentDecisionResponseDTO
            {
                Decision = request.Decision,
                Booking = caseRecord.Booking, // Could be mapped to a smaller DTO
                CaseStatus = caseRecord.Status,
                Refund = request.Decision == "Cancel" ? new RefundPreviewDTO
                {
                    Amount = caseRecord.Booking.FinalAmount,
                    Destination = "Wallet",
                    PointsRestored = caseRecord.Booking.PointsUsed,
                    OriginalVoucherRestored = caseRecord.Booking.AppliedVoucherId.HasValue
                } : null,
                CompensationVoucher = voucher != null ? new CompensationVoucherDTO
                {
                    VoucherId = voucher.VoucherId,
                    DiscountPercent = 20,
                    ExpiresAt = voucher.ExpiryDate.ToString("yyyy-MM-dd")
                } : null
            };
        }

        public async Task SystemCancelAsync(int userId, int bookingId, long caseId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .FirstOrDefaultAsync(c => c.Id == caseId && c.BookingId == bookingId && c.UserId == userId);

            if (caseRecord == null)
                throw new NotFoundException("Affected booking record not found.");

            await HandleCancelDecisionAsync(caseRecord, now);
            await IssueCompensationVoucherAsync(userId, caseRecord, now);
        }

        private async Task HandleCancelDecisionAsync(IncidentAffectedBooking caseRecord, DateTime now)
        {
            var booking = caseRecord.Booking;
            

            if (booking.FinalAmount > 0)
            {
                await _walletService.RefundBalanceAsync(booking.UserId ?? 0, booking.FinalAmount, "Refund - Incident Cancel");
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = "MoneyRefund",
                    Amount = booking.FinalAmount,
                    CreatedAtVn = now
                });
            }


            if (booking.PointsUsed > 0)
            {
                var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == booking.UserId);
                if (profile != null)
                {
                    profile.TotalPoint += booking.PointsUsed;
                    _context.PointLedgers.Add(new PointLedger
                    {
                        UserId = booking.UserId ?? 0,
                        PointsAdded = booking.PointsUsed,
                        Reason = "Refund: Incident cancellation points refund",
                        TransactionDate = now
                    });
                }
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = "PointsRefund",
                    Amount = booking.PointsUsed,
                    CreatedAtVn = now
                });
            }


            if (booking.AppliedVoucherId.HasValue)
            {
                var userVoucher = await _context.UserVouchers
                    .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.VoucherId == booking.AppliedVoucherId.Value);
                if (userVoucher != null)
                {
                    userVoucher.IsUsed = false;
                    userVoucher.UsedDate = null;
                }
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = "VoucherReturn",
                    CreatedAtVn = now
                });
            }


            booking.Status = "Cancelled";
            booking.UpdatedAt = now;
            if (booking.ScheduledTime.Date == now.Date)
            {
                var oldCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(c => c.BranchId == booking.BranchId && c.Date == booking.ScheduledTime.Date && c.TimeSlot.StartTime <= booking.ScheduledTime.TimeOfDay && c.TimeSlot.EndTime > booking.ScheduledTime.TimeOfDay);

                if (oldCapacity != null)
                {
                    oldCapacity.BookedWeight -= (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1);
                    if (oldCapacity.BookedWeight < 0) oldCapacity.BookedWeight = 0;
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == booking.UserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                var subject = $"LuxeWash - Thông báo hủy lịch do sự cố hệ thống";
                var body = $"Chào bạn,<br/><br/>Lịch đặt rửa xe của bạn vào lúc {booking.ScheduledTime:dd/MM/yyyy HH:mm} đã bị hủy do sự cố bất khả kháng tại chi nhánh.<br/>Chúng tôi đã hoàn lại toàn bộ số tiền <b>{booking.FinalAmount} VNĐ</b> và <b>{booking.PointsUsed} điểm</b> vào tài khoản của bạn.<br/><br/>Ngoài ra, hệ thống đã gửi tặng bạn một Voucher giảm giá 20% như một lời xin lỗi chân thành nhất.<br/><br/>Xin lỗi bạn vì sự bất tiện này!";
                _ = Task.Run(() => _emailService.SendEmailAsync(user.Email, subject, body));
            }

            caseRecord.Status = "Cancelled";
            caseRecord.Decision = "Cancel";
            caseRecord.DecidedAtVn = now;
        }

        private async Task HandleTransferDecisionAsync(int userId, IncidentAffectedBooking caseRecord, int targetBranchId, int targetSlotId, DateTime now)
        {
            var originalBooking = caseRecord.Booking;
            

            var targetSlot = await _context.TimeSlots.FindAsync(targetSlotId);
            if (targetSlot == null || targetSlot.BranchId != targetBranchId)
                throw new BadRequestException("Invalid target slot/branch.");

            var vehicle = await _context.Vehicles.FindAsync(originalBooking.VehicleId);
            var bookingDetails = await _context.BookingDetails.Where(d => d.BookingId == originalBooking.BookingId).ToListAsync();

            var ctx = new BookingContextDTO
            {
                IsBusiness = originalBooking.BusinessProfileId.HasValue,
                IsVipEligible = false, 
                VehicleTypeId = vehicle?.VehicleTypeId,
                ServiceIds = bookingDetails.Select(d => d.ServiceId).ToList(),
                CapacityWeight = originalBooking.CapacityWeight > 0 ? originalBooking.CapacityWeight : 1
            };

            var targetDate = originalBooking.ScheduledTime.Date;
            var cap = await _capacityService.GetEffectiveSlotCapacityAsync(targetBranchId, targetDate, targetSlotId, ctx, now);

            if (cap.AvailableWeight < ctx.CapacityWeight)
            {
                throw new ConflictException("DESTINATION_CAPACITY_CHANGED");
            }


            var oldCapacityRows = await _context.DailySlotCapacities
                .Where(c => c.BranchId == originalBooking.BranchId && c.Date == originalBooking.ScheduledTime.Date && c.TimeSlot.StartTime <= originalBooking.ScheduledTime.TimeOfDay && c.TimeSlot.EndTime > originalBooking.ScheduledTime.TimeOfDay)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.BookedWeight, c => Math.Max(0, c.BookedWeight - ctx.CapacityWeight)));

            var newCapacityRows = await _context.DailySlotCapacities
                .Where(c => c.SlotId == targetSlotId && c.BranchId == targetBranchId && c.Date == targetDate && (c.BookedWeight + ctx.CapacityWeight <= cap.EffectiveCapacity))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.BookedWeight, c => c.BookedWeight + ctx.CapacityWeight));

            if (newCapacityRows == 0)
            {
                var exists = await _context.DailySlotCapacities.AnyAsync(c => c.SlotId == targetSlotId && c.BranchId == targetBranchId && c.Date == targetDate);
                if (exists)
                {
                    throw new ConflictException("DESTINATION_CAPACITY_CHANGED");
                }
                else
                {
                    var newCapacity = new AutoWashPro.DAL.Entities.DailySlotCapacity
                    {
                        SlotId = targetSlotId,
                        BranchId = targetBranchId,
                        Date = targetDate,
                        BookedWeight = ctx.CapacityWeight
                    };
                    _context.DailySlotCapacities.Add(newCapacity);
                }
            }

            originalBooking.BranchId = targetBranchId;

            originalBooking.ScheduledTime = originalBooking.ScheduledTime.Date.Add(targetSlot.StartTime);
            originalBooking.UpdatedAt = now;

            caseRecord.Status = "Transferred";
            caseRecord.Decision = "Transfer";
            caseRecord.TargetBranchId = targetBranchId;
            caseRecord.TargetSlotId = targetSlotId;
            caseRecord.DecidedAtVn = now;
        }

        private async Task<UserVoucher> IssueCompensationVoucherAsync(int userId, IncidentAffectedBooking caseRecord, DateTime now)
        {

            var sysVoucherCode = "INCIDENT_COMP_20";
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == sysVoucherCode);
            if (voucher == null)
            {
                throw new InvalidOperationException("System compensation voucher missing.");
            }

            var uv = new UserVoucher
            {
                UserId = userId,
                VoucherId = voucher.VoucherId,
                IsUsed = false,
                ReceivedDate = now,
                ExpiryDate = now.AddMonths(6),
                SourceIncidentAffectedBookingId = caseRecord.Id
            };
            _context.UserVouchers.Add(uv);

            _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
            {
                CaseId = caseRecord.Id,
                Kind = "CompensationVoucher",
                CreatedAtVn = now
            });

            return uv;
        }

        public async Task<IncidentOptionsResponseDTO?> GetIncidentOptionsAsync(int userId, int bookingId)
        {
            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Booking)
                .ThenInclude(b => b.Vehicle)
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .Include(c => c.Incident)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.UserId == userId);

            if (caseRecord == null)
                return null;

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == caseRecord.OriginalBranchId);

            var result = new IncidentOptionsResponseDTO
            {
                CaseId = caseRecord.Id,
                IncidentId = caseRecord.IncidentId,
                CaseStatus = caseRecord.Status,
                OriginalBooking = new { BookingId = caseRecord.BookingId, ScheduledTime = caseRecord.OriginalScheduledTimeVn.ToString("yyyy-MM-dd HH:mm"), LicensePlate = caseRecord.Booking?.Vehicle?.LicensePlate },
                Reason = caseRecord.Incident?.Reason ?? "System incident",
                Eta = caseRecord.Incident?.EstimatedEndAtVn.ToString("yyyy-MM-dd HH:mm") ?? "",
                ResponseDeadlineAt = caseRecord.ResponseDeadlineAtVn.ToString("yyyy-MM-dd HH:mm"),
                AllowedActions = new List<string> { "Cancel" }, // "Keep" is conditional, "Transfer" is conditional
                Version = caseRecord.Incident?.Version ?? 1,
                RefundPreview = new RefundPreviewDTO
                {
                    Amount = caseRecord.Booking?.FinalAmount ?? 0,
                    Destination = "Wallet",
                    PointsRestored = caseRecord.Booking?.PointsUsed ?? 0,
                    OriginalVoucherRestored = caseRecord.Booking?.AppliedVoucherId.HasValue ?? false
                }
            };

            if (caseRecord.Status == "AwaitingCustomer")
            {
                var originalBranchId = caseRecord.OriginalBranchId;
                var targetDate = caseRecord.OriginalScheduledTimeVn.Date;
                var vehicleTypeId = caseRecord.Booking?.Vehicle?.VehicleTypeId ?? 1;
                var serviceIds = caseRecord.Booking?.BookingDetails.Select(d => d.ServiceId).ToList() ?? new System.Collections.Generic.List<int>();

                var otherBranches = await _context.Branches.Where(b => b.BranchId != originalBranchId && b.IsActive).ToListAsync();
                var otherBranchIds = otherBranches.Select(b => b.BranchId).ToList();

                var allAltSlots = await _context.TimeSlots
                    .Where(s => otherBranchIds.Contains(s.BranchId))
                    .ToListAsync();

                var allAltDailyCaps = await _context.DailySlotCapacities
                    .Where(dc => otherBranchIds.Contains(dc.BranchId) && dc.Date == targetDate.Date)
                    .ToListAsync();

                var allActiveIncidents = await _context.BranchIncidents
                    .Include(i => i.IncidentLanes)
                    .Where(i => otherBranchIds.Contains(i.BranchId) && i.Status == "Active" 
                                && i.StartedAtVn < targetDate.Date.AddDays(1) 
                                && i.EstimatedEndAtVn > targetDate.Date)
                    .ToListAsync();

                var capacityWeight = caseRecord.Booking?.CapacityWeight > 0 ? caseRecord.Booking.CapacityWeight : 1;
                bool canTransfer = false;
                var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;

                foreach (var b in otherBranches)
                {
                    var branchSlots = allAltSlots.Where(s => s.BranchId == b.BranchId).OrderBy(s => s.StartTime).ToList();
                    var branchCaps = allAltDailyCaps.Where(c => c.BranchId == b.BranchId).ToDictionary(c => c.SlotId, c => c.BookedWeight);
                    var branchIncidents = allActiveIncidents.Where(i => i.BranchId == b.BranchId).ToList();

                    foreach (var s in branchSlots)
                    {
                        var slotStart = targetDate.Date.Add(s.StartTime);
                        var slotEnd = targetDate.Date.Add(s.EndTime);
                        if (s.EndTime <= s.StartTime) slotEnd = slotEnd.AddDays(1);

                        // Skip past slots if targetDate is today
                        if (slotStart <= now) continue;

                        var incident = branchIncidents.FirstOrDefault(i => i.StartedAtVn < slotEnd && i.EstimatedEndAtVn > slotStart);
                        int availableWeight = s.MaxCapacity;
                        
                        if (incident != null)
                        {
                            if (incident.Scope == "WholeBranch") availableWeight = 0;
                            else if (incident.Scope == "SelectedLanes" && incident.IncidentLanes != null)
                            {
                                 availableWeight = Math.Max(0, s.MaxCapacity - incident.IncidentLanes.Count);
                            }
                        }

                        int bookedWeight = branchCaps.ContainsKey(s.SlotId) ? branchCaps[s.SlotId] : 0;
                        availableWeight -= bookedWeight;

                        if (availableWeight >= capacityWeight)
                        {
                            canTransfer = true;
                            string start = s.StartTime.ToString(@"hh\:mm");
                            string end = s.EndTime.ToString(@"hh\:mm");
                            result.Alternatives.Add(new IncidentAlternativeDTO
                            {
                                BranchId = b.BranchId,
                                BranchName = b.Name,
                                SlotId = s.SlotId,
                                StartAt = start,
                                EndAt = end,
                                DistanceKm = 0,
                                AvailableWeight = availableWeight
                            });
                        }
                    }
                }
                
                if (canTransfer)
                {
                    result.AllowedActions.Add("Transfer");
                }
            }

            return result;
        }
    }
}
