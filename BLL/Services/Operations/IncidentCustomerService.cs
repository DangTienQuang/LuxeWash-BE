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
using BLL.Helpers;
using BLL.DTOs.Business;
using BLL.Services.Interface;

namespace AutoWashPro.BLL.Services
{
    public class IncidentCustomerService : IIncidentCustomerService
    {
        private readonly AutoWashDbContext _context;
        private readonly IBookingService _bookingService;
        private readonly IWalletService _walletService;
        private readonly IIncidentCapacityService _capacityService;
        private readonly IEmailService _emailService;
        private readonly ILaneSchedulerService _laneSchedulerService;

        public IncidentCustomerService(
            AutoWashDbContext context, 
            IBookingService bookingService,
            IWalletService walletService,
            IIncidentCapacityService capacityService,
            IEmailService emailService,
            ILaneSchedulerService laneSchedulerService)
        {
            _context = context;
            _bookingService = bookingService;
            _walletService = walletService;
            _capacityService = capacityService;
            _emailService = emailService;
            _laneSchedulerService = laneSchedulerService;
        }

        public async Task<IncidentDecisionResponseDTO> ProcessIncidentDecisionAsync(
            int userId,
            int bookingId,
            IncidentDecisionRequestDTO request,
            string? idempotencyKey = null)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var normalizedIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
                ? null
                : idempotencyKey.Trim();
            if (normalizedIdempotencyKey?.Length > 100)
                throw new BadRequestException("Idempotency-Key must not exceed 100 characters.");

            await using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            // Claim the case with one atomic UPDATE. A customer request, a network
            // retry and the timeout worker can no longer all observe AwaitingCustomer
            // and execute the same financial/capacity operation twice.
            var claimedRows = await _context.IncidentAffectedBookings
                .Where(c =>
                    c.Id == request.CaseId &&
                    c.IncidentId == request.IncidentId &&
                    c.BookingId == bookingId &&
                    c.UserId == userId &&
                    c.Status == "AwaitingCustomer")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.Status, "ProcessingDecision")
                    .SetProperty(c => c.DecisionIdempotencyKey, normalizedIdempotencyKey));

            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Incident)
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .FirstOrDefaultAsync(c =>
                    c.Id == request.CaseId &&
                    c.IncidentId == request.IncidentId &&
                    c.BookingId == bookingId &&
                    c.UserId == userId);

            if (caseRecord == null)
                throw new NotFoundException("Affected booking record not found.");

            if (claimedRows == 0)
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
                    existingVoucher ??= await _context.UserVouchers
                        .FirstOrDefaultAsync(uv => uv.SourceIncidentAffectedBookingId == caseRecord.Id);
                    
                    return new IncidentDecisionResponseDTO
                    {
                        Decision = caseRecord.Decision,
                        Booking = ToDecisionBookingDTO(caseRecord.Booking),
                        CaseStatus = caseRecord.Status,
                        Refund = caseRecord.Decision == "Cancel"
                            ? BuildRefundPreview(caseRecord.Booking)
                            : null,
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

            if (caseRecord.Incident != null && caseRecord.Incident.Version != request.ExpectedVersion)
                throw new ConflictException("INCIDENT_VERSION_CHANGED");

            if (now > caseRecord.ResponseDeadlineAtVn)
                throw new BadRequestException("Response deadline has expired.");

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
                caseRecord.ActiveBookingId = null;
            }
            else
            {
                throw new BadRequestException("Invalid decision.");
            }


            UserVoucher? voucher = null;
            if (request.Decision != "Keep" && !IsBusinessBooking(caseRecord.Booking))
            {
                voucher = await IssueCompensationVoucherAsync(userId, caseRecord, now);
            }
            
            await _context.SaveChangesAsync();
            if (voucher != null)
            {
                caseRecord.CompensationUserVoucherId = voucher.Id;
                await _context.SaveChangesAsync();
            }
            await transaction.CommitAsync();

            return new IncidentDecisionResponseDTO
            {
                Decision = request.Decision,
                Booking = ToDecisionBookingDTO(caseRecord.Booking),
                CaseStatus = caseRecord.Status,
                Refund = request.Decision == "Cancel"
                    ? BuildRefundPreview(caseRecord.Booking)
                    : null,
                CompensationVoucher = voucher != null ? new CompensationVoucherDTO
                {
                    VoucherId = voucher.VoucherId,
                    DiscountPercent = 20,
                    ExpiresAt = voucher.ExpiryDate.ToString("yyyy-MM-dd")
                } : null
            };
        }

        private static IncidentDecisionBookingDTO ToDecisionBookingDTO(Booking booking)
        {
            return new IncidentDecisionBookingDTO
            {
                BookingId = booking.BookingId,
                BranchId = booking.BranchId,
                ScheduledTime = booking.ScheduledTime,
                Status = booking.Status
            };
        }

        private static bool IsBusinessBooking(Booking booking) =>
            booking.BusinessProfileId.HasValue ||
            string.Equals(booking.BookingType, "Business", StringComparison.OrdinalIgnoreCase);

        private static RefundPreviewDTO BuildRefundPreview(Booking booking)
        {
            if (IsBusinessBooking(booking))
            {
                return new RefundPreviewDTO
                {
                    // Business bookings are charged against the monthly committed
                    // credit, not the customer's wallet. Cancelling releases that
                    // commitment; it must never create wallet money.
                    Amount = booking.FinalAmount,
                    Destination = "BusinessCredit",
                    PointsRestored = 0,
                    OriginalVoucherRestored = false
                };
            }

            return new RefundPreviewDTO
            {
                Amount = booking.FinalAmount,
                Destination = "Wallet",
                PointsRestored = booking.PointsUsed,
                OriginalVoucherRestored = booking.AppliedVoucherId.HasValue
            };
        }

        public async Task SystemCancelAsync(int userId, int bookingId, long caseId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                : null;

            try
            {
                var claimedRows = await _context.IncidentAffectedBookings
                    .Where(c =>
                        c.Id == caseId &&
                        c.BookingId == bookingId &&
                        c.UserId == userId &&
                        c.Status == "AwaitingCustomer")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(c => c.Status, "ProcessingSystemCancel"));

                var caseRecord = await _context.IncidentAffectedBookings
                    .Include(c => c.Booking)
                    .ThenInclude(b => b.BookingDetails)
                    .FirstOrDefaultAsync(c => c.Id == caseId && c.BookingId == bookingId && c.UserId == userId);

                if (caseRecord == null)
                    throw new NotFoundException("Affected booking record not found.");

                // A customer may submit a decision at the same time that the timeout
                // worker runs. Do not refund or issue compensation twice.
                if (claimedRows == 0)
                {
                    if (caseRecord.Status == "Cancelled" && caseRecord.Decision == "Cancel")
                    {
                        return;
                    }

                    throw new ConflictException("DECISION_ALREADY_FINAL");
                }

                await HandleCancelDecisionAsync(caseRecord, now);
                UserVoucher? voucher = null;
                if (!IsBusinessBooking(caseRecord.Booking))
                {
                    voucher = await IssueCompensationVoucherAsync(userId, caseRecord, now);
                }
                await _context.SaveChangesAsync();
                if (voucher != null)
                {
                    caseRecord.CompensationUserVoucherId = voucher.Id;
                    await _context.SaveChangesAsync();
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                throw;
            }
        }

        private async Task HandleCancelDecisionAsync(IncidentAffectedBooking caseRecord, DateTime now)
        {
            var booking = caseRecord.Booking;
            var isBusiness = IsBusinessBooking(booking);

            if (!isBusiness && booking.FinalAmount > 0)
            {
                await _walletService.RefundBalanceAsync(booking.UserId ?? 0, booking.FinalAmount, "Refund - Incident Cancel");
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = IncidentFinancialOperationKinds.MoneyRefund,
                    Amount = booking.FinalAmount,
                    CreatedAtVn = now
                });
            }


            if (!isBusiness && booking.PointsUsed > 0)
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
                    Kind = IncidentFinancialOperationKinds.PointsRefund,
                    Amount = booking.PointsUsed,
                    CreatedAtVn = now
                });
            }


            if (!isBusiness && booking.AppliedVoucherId.HasValue)
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
                    Kind = IncidentFinancialOperationKinds.RestoreOriginalVoucher,
                    CreatedAtVn = now
                });
            }

            if (isBusiness && booking.FinalAmount > 0)
            {
                // Fleet incidents affect future Pending/Confirmed bookings, before a
                // FleetWashLog or monthly invoice item exists. Cancelling the booking
                // releases the commitment because credit calculation excludes
                // Cancelled bookings; this operation is the explicit audit trail.
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = IncidentFinancialOperationKinds.BusinessCreditRelease,
                    Amount = booking.FinalAmount,
                    CreatedAtVn = now
                });
            }


            booking.Status = "Cancelled";
            booking.UpdatedAt = now;
            var oldCapacity = await _context.DailySlotCapacities
                .FirstOrDefaultAsync(c => c.BranchId == booking.BranchId &&
                    c.Date == booking.ScheduledTime.Date &&
                    c.TimeSlot.StartTime <= booking.ScheduledTime.TimeOfDay &&
                    c.TimeSlot.EndTime > booking.ScheduledTime.TimeOfDay);

            if (oldCapacity != null)
            {
                oldCapacity.BookedWeight -= (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1);
                if (oldCapacity.BookedWeight < 0) oldCapacity.BookedWeight = 0;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == booking.UserId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                var subject = $"LuxeWash - Thông báo hủy lịch do sự cố hệ thống";
                var body = isBusiness
                    ? $"Chào bạn,<br/><br/>Lịch đặt xe {booking.LicensePlate} vào lúc {booking.ScheduledTime:dd/MM/yyyy HH:mm} đã bị hủy do sự cố bất khả kháng tại chi nhánh.<br/>Khoản cam kết <b>{booking.FinalAmount:N0} VNĐ</b> đã được giải phóng khỏi hạn mức tháng của doanh nghiệp.<br/><br/>Xin lỗi bạn vì sự bất tiện này!"
                    : $"Chào bạn,<br/><br/>Lịch đặt rửa xe của bạn vào lúc {booking.ScheduledTime:dd/MM/yyyy HH:mm} đã bị hủy do sự cố bất khả kháng tại chi nhánh.<br/>Chúng tôi đã hoàn lại toàn bộ số tiền <b>{booking.FinalAmount} VNĐ</b> và <b>{booking.PointsUsed} điểm</b> vào tài khoản của bạn.<br/><br/>Ngoài ra, hệ thống đã gửi tặng bạn một Voucher giảm giá 20% như một lời xin lỗi chân thành nhất.<br/><br/>Xin lỗi bạn vì sự bất tiện này!";
                _ = Task.Run(() => _emailService.SendEmailAsync(user.Email, subject, body));
            }

            caseRecord.Status = "Cancelled";
            caseRecord.Decision = "Cancel";
            caseRecord.DecidedAtVn = now;
            caseRecord.ActiveBookingId = null;
        }

        private async Task HandleTransferDecisionAsync(int userId, IncidentAffectedBooking caseRecord, int targetBranchId, int targetSlotId, DateTime now)
        {
            var originalBooking = caseRecord.Booking;

            if (targetBranchId == originalBooking.BranchId)
                throw new BadRequestException("The destination branch must be different from the affected branch.");

            var targetSlot = await _context.TimeSlots.FindAsync(targetSlotId);
            if (targetSlot == null || targetSlot.BranchId != targetBranchId)
                throw new BadRequestException("Invalid target slot/branch.");

            var targetBranchIsActive = await _context.Branches
                .AnyAsync(b => b.BranchId == targetBranchId && b.IsActive);
            if (!targetBranchIsActive)
                throw new BadRequestException("The destination branch is not active.");

            var vehicle = await _context.Vehicles.FindAsync(originalBooking.VehicleId);
            var fleetVehicle = originalBooking.FleetVehicleId.HasValue
                ? await _context.FleetVehicles
                    .Include(v => v.VehicleType)
                    .FirstOrDefaultAsync(v => v.FleetVehicleId == originalBooking.FleetVehicleId.Value)
                : null;
            var bookingDetails = await _context.BookingDetails.Where(d => d.BookingId == originalBooking.BookingId).ToListAsync();
            var customerProfile = await _context.CustomerProfiles
                .Include(profile => profile.Tier)
                .FirstOrDefaultAsync(profile => profile.UserId == userId);
            var vehicleTypeId = originalBooking.ActualVehicleTypeId ??
                fleetVehicle?.VehicleTypeId ??
                vehicle?.VehicleTypeId;

            if (!vehicleTypeId.HasValue)
                throw new BadRequestException("Unable to determine the vehicle type for this booking.");

            var serviceIds = bookingDetails.Select(d => d.ServiceId).Distinct().ToList();
            var targetServicePrices = await _context.ServicePrices
                .Where(p => p.BranchId == targetBranchId &&
                    p.VehicleTypeId == vehicleTypeId.Value &&
                    serviceIds.Contains(p.ServiceId))
                .ToListAsync();
            var supportedServiceCount = targetServicePrices.Select(p => p.ServiceId).Distinct().Count();
            if (supportedServiceCount != serviceIds.Count)
                throw new BadRequestException("The destination branch does not support all services for this vehicle.");

            var ctx = new BookingContextDTO
            {
                IsBusiness = originalBooking.BusinessProfileId.HasValue,
                IsVipEligible = CustomerEligibilityHelper.IsVipEligible(customerProfile),
                VehicleTypeId = vehicleTypeId,
                ServiceIds = serviceIds,
                CapacityWeight = originalBooking.CapacityWeight > 0 ? originalBooking.CapacityWeight : 1
            };

            var targetDate = originalBooking.ScheduledTime.Date;
            var targetStart = targetDate.Add(targetSlot.StartTime);
            var targetScheduledTime = targetStart;
            if (targetStart <= now)
                throw new BadRequestException("The destination time slot has already started.");
            var cap = await _capacityService.GetEffectiveSlotCapacityAsync(targetBranchId, targetDate, targetSlotId, ctx, now);

            if (cap.AvailableWeight < ctx.CapacityWeight)
            {
                throw new ConflictException("DESTINATION_CAPACITY_CHANGED");
            }

            if (IsBusinessBooking(originalBooking))
            {
                if (fleetVehicle?.VehicleType == null)
                    throw new BadRequestException("Fleet vehicle information is incomplete.");

                var schedule = await _laneSchedulerService.ScheduleFleetAcrossSlotsAsync(
                    targetBranchId,
                    targetDate,
                    targetSlotId,
                    new List<VehicleScheduleRequest>
                    {
                        new VehicleScheduleRequest
                        {
                            FleetVehicleId = fleetVehicle.FleetVehicleId,
                            VehicleType = fleetVehicle.VehicleType,
                            ServicePrices = targetServicePrices,
                            CapacityWeight = ctx.CapacityWeight
                        }
                    },
                    excludedBookingId: originalBooking.BookingId);

                var assignment = schedule.Assignments.FirstOrDefault();
                if (!schedule.Success || assignment == null || assignment.AssignedSlotId != targetSlotId)
                    throw new ConflictException("DESTINATION_CAPACITY_CHANGED");

                // Fleet vehicles can start later than the slot boundary because
                // the business scheduler sequences vehicles on the same lane.
                targetScheduledTime = assignment.EstimatedStart;
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

            originalBooking.ScheduledTime = targetScheduledTime;
            originalBooking.UpdatedAt = now;

            caseRecord.Status = "Transferred";
            caseRecord.Decision = "Transfer";
            caseRecord.TargetBranchId = targetBranchId;
            caseRecord.TargetSlotId = targetSlotId;
            caseRecord.DecidedAtVn = now;
            caseRecord.ActiveBookingId = null;
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
                Kind = IncidentFinancialOperationKinds.CompensationVoucher,
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
                .ThenInclude(b => b.FleetVehicle)
                .ThenInclude(v => v!.VehicleType)
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .Include(c => c.Incident)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.UserId == userId);

            if (caseRecord == null)
                return null;

            var targetBranch = caseRecord.TargetBranchId.HasValue
                ? await _context.Branches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BranchId == caseRecord.TargetBranchId.Value)
                : null;
            var targetSlot = caseRecord.TargetSlotId.HasValue
                ? await _context.TimeSlots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SlotId == caseRecord.TargetSlotId.Value)
                : null;

            var result = new IncidentOptionsResponseDTO
            {
                CaseId = caseRecord.Id,
                IncidentId = caseRecord.IncidentId,
                CaseStatus = caseRecord.Status,
                OriginalBooking = new { BookingId = caseRecord.BookingId, ScheduledTime = caseRecord.OriginalScheduledTimeVn.ToString("yyyy-MM-dd HH:mm"), LicensePlate = caseRecord.Booking?.LicensePlate },
                Reason = caseRecord.Incident?.Reason ?? "System incident",
                Eta = caseRecord.Incident?.EstimatedEndAtVn.ToString("yyyy-MM-dd HH:mm") ?? "",
                ResponseDeadlineAt = caseRecord.ResponseDeadlineAtVn.ToString("yyyy-MM-dd HH:mm"),
                AllowedActions = new List<string>(),
                Version = caseRecord.Incident?.Version ?? 1,
                RefundPreview = caseRecord.Booking != null
                    ? BuildRefundPreview(caseRecord.Booking)
                    : new RefundPreviewDTO(),
                VoucherTerms = IsBusinessBooking(caseRecord.Booking!)
                    ? new VoucherTermsDTO
                    {
                        IsEligible = false,
                        DiscountPercent = 0,
                        Code = string.Empty,
                        Message = "Quyết định xử lý sự cố Fleet sẽ giải phóng hạn mức cam kết nhưng không phát hành voucher cá nhân."
                    }
                    : new VoucherTermsDTO(),
                TargetBranchId = caseRecord.TargetBranchId,
                TargetBranchName = targetBranch?.Name,
                TargetSlotId = caseRecord.TargetSlotId,
                TargetSlotLabel = targetSlot == null
                    ? null
                    : $"{targetSlot.StartTime:hh\\:mm}–{targetSlot.EndTime:hh\\:mm}",
                TargetScheduledTime = caseRecord.Status == "Transferred"
                    ? caseRecord.Booking!.ScheduledTime.ToString("yyyy-MM-dd HH:mm")
                    : null
            };

            if (caseRecord.Status == "AwaitingCustomer")
            {
                result.AllowedActions.Add("Cancel");
                var originalBranchId = caseRecord.OriginalBranchId;
                var targetDate = caseRecord.OriginalScheduledTimeVn.Date;
                var vehicleTypeId = caseRecord.Booking?.ActualVehicleTypeId ??
                    caseRecord.Booking?.FleetVehicle?.VehicleTypeId ??
                    caseRecord.Booking?.Vehicle?.VehicleTypeId;
                var serviceIds = caseRecord.Booking?.BookingDetails.Select(d => d.ServiceId).ToList() ?? new System.Collections.Generic.List<int>();

                var otherBranches = await _context.Branches.Where(b => b.BranchId != originalBranchId && b.IsActive).ToListAsync();
                var otherBranchIds = otherBranches.Select(b => b.BranchId).ToList();

                var allAltSlots = await _context.TimeSlots
                    .Where(s => otherBranchIds.Contains(s.BranchId))
                    .ToListAsync();

                var allTargetServicePrices = vehicleTypeId.HasValue
                    ? await _context.ServicePrices
                        .Where(p => otherBranchIds.Contains(p.BranchId) &&
                            p.VehicleTypeId == vehicleTypeId.Value &&
                            serviceIds.Contains(p.ServiceId))
                        .ToListAsync()
                    : new List<ServicePrice>();
                var targetPricesByBranch = allTargetServicePrices
                    .GroupBy(price => price.BranchId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                var isBusinessBooking = IsBusinessBooking(caseRecord.Booking!);

                var capacityWeight = caseRecord.Booking?.CapacityWeight > 0 ? caseRecord.Booking.CapacityWeight : 1;
                bool canTransfer = false;
                var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;

                foreach (var b in otherBranches)
                {
                    if (!vehicleTypeId.HasValue) continue;

                    var targetServicePrices = targetPricesByBranch.TryGetValue(b.BranchId, out var branchPrices)
                        ? branchPrices
                        : new List<ServicePrice>();
                    var supportedServiceCount = targetServicePrices
                        .Select(p => p.ServiceId)
                        .Distinct()
                        .Count();
                    if (supportedServiceCount != serviceIds.Distinct().Count()) continue;

                    var branchSlots = allAltSlots.Where(s => s.BranchId == b.BranchId).OrderBy(s => s.StartTime).ToList();
                    var bookingContext = new BookingContextDTO
                    {
                        IsBusiness = isBusinessBooking,
                        IsVipEligible = false,
                        VehicleTypeId = vehicleTypeId,
                        ServiceIds = serviceIds,
                        CapacityWeight = capacityWeight
                    };

                    // The Fleet scheduler already evaluates capacity, active incidents,
                    // lane reservations and every following slot. Calling it once per
                    // slot made incident-options grow quadratically and could time out.
                    // Start at the original appointment time (or the first later slot)
                    // so the suggested transfer preserves the customer's schedule instead
                    // of always returning the branch's earliest opening slot.
                    if (isBusinessBooking)
                    {
                        var originalStartTime = caseRecord.OriginalScheduledTimeVn.TimeOfDay;
                        var preferredSlot = branchSlots.FirstOrDefault(slot =>
                                targetDate.Date.Add(slot.StartTime) > now &&
                                slot.StartTime >= originalStartTime)
                            ?? branchSlots.FirstOrDefault(slot =>
                                targetDate.Date.Add(slot.StartTime) > now);
                        var fleetVehicle = caseRecord.Booking!.FleetVehicle;
                        if (preferredSlot == null || fleetVehicle?.VehicleType == null)
                            continue;

                        var schedule = await _laneSchedulerService.ScheduleFleetAcrossSlotsAsync(
                            b.BranchId,
                            targetDate,
                            preferredSlot.SlotId,
                            new List<VehicleScheduleRequest>
                            {
                                new VehicleScheduleRequest
                                {
                                    FleetVehicleId = fleetVehicle.FleetVehicleId,
                                    VehicleType = fleetVehicle.VehicleType,
                                    ServicePrices = targetServicePrices,
                                    CapacityWeight = capacityWeight
                                }
                            });
                        var assignment = schedule.Assignments.FirstOrDefault();
                        var assignedSlot = assignment == null
                            ? null
                            : branchSlots.FirstOrDefault(slot => slot.SlotId == assignment.AssignedSlotId);
                        if (!schedule.Success || assignment == null || assignedSlot == null)
                            continue;

                        var capacity = await _capacityService.GetEffectiveSlotCapacityAsync(
                            b.BranchId,
                            targetDate,
                            assignedSlot.SlotId,
                            bookingContext,
                            now);
                        if (capacity.AvailableWeight < capacityWeight)
                            continue;

                        canTransfer = true;
                        result.Alternatives.Add(new IncidentAlternativeDTO
                        {
                            BranchId = b.BranchId,
                            BranchName = b.Name,
                            SlotId = assignedSlot.SlotId,
                            StartAt = assignment.EstimatedStart.ToString("HH:mm"),
                            EndAt = assignment.EstimatedEnd.ToString("HH:mm"),
                            DistanceKm = 0,
                            AvailableWeight = capacity.AvailableWeight
                        });
                        continue;
                    }

                    foreach (var s in branchSlots)
                    {
                        var slotStart = targetDate.Date.Add(s.StartTime);

                        // Skip past slots if targetDate is today
                        if (slotStart <= now) continue;

                        var capacity = await _capacityService.GetEffectiveSlotCapacityAsync(
                            b.BranchId,
                            targetDate,
                            s.SlotId,
                            bookingContext,
                            now);
                        int availableWeight = capacity.AvailableWeight;

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
