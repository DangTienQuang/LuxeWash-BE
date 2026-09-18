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

        public IncidentCustomerService(
            AutoWashDbContext context, 
            IBookingService bookingService,
            IWalletService walletService,
            IIncidentCapacityService capacityService)
        {
            _context = context;
            _bookingService = bookingService;
            _walletService = walletService;
            _capacityService = capacityService;
        }

        public async Task HandleCustomerDecisionAsync(int userId, long affectedBookingId, string decision, int? targetBranchId, int? targetSlotId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .FirstOrDefaultAsync(c => c.Id == affectedBookingId && c.UserId == userId);

            if (caseRecord == null)
                throw new NotFoundException("Affected booking record not found.");
            
            if (caseRecord.Status != "AwaitingCustomer")
                throw new BadRequestException($"Cannot make decision. Status is currently: {caseRecord.Status}");

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            if (decision == "Cancel")
            {
                await HandleCancelDecisionAsync(caseRecord, now);
            }
            else if (decision == "Transfer")
            {
                if (targetBranchId == null || targetSlotId == null)
                    throw new BadRequestException("Target branch and slot must be provided for Transfer.");

                await HandleTransferDecisionAsync(userId, caseRecord, targetBranchId.Value, targetSlotId.Value, now);
            }
            else
            {
                throw new BadRequestException("Invalid decision.");
            }

            // Issue compensation voucher 20% / 6 months
            var voucher = await IssueCompensationVoucherAsync(userId, caseRecord, now);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task SystemCancelAsync(long affectedBookingId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Booking)
                .FirstOrDefaultAsync(c => c.Id == affectedBookingId);

            if (caseRecord == null || caseRecord.Status != "AwaitingCustomer")
                return;

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            caseRecord.Status = "CancelledBySystem";
            caseRecord.Decision = "Cancel";
            caseRecord.DecidedAtVn = now;

            var booking = caseRecord.Booking;
            booking.Status = "Cancelled";
            booking.UpdatedAt = now;

            // Refund Money
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

            // Refund Points
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

            // Restore Voucher
            if (booking.AppliedVoucherId.HasValue)
            {
                var uv = await _context.UserVouchers.FirstOrDefaultAsync(v => v.UserId == booking.UserId && v.VoucherId == booking.AppliedVoucherId && v.IsUsed);
                if (uv != null)
                {
                    uv.IsUsed = false;
                }
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = "RestoreOriginalVoucher",
                    ExternalReference = booking.AppliedVoucherId.ToString(),
                    CreatedAtVn = now
                });
            }

            if (booking.UserId.HasValue)
            {
                var voucher = await IssueCompensationVoucherAsync(booking.UserId.Value, caseRecord, now);
                caseRecord.CompensationUserVoucherId = voucher.Id;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private async Task HandleCancelDecisionAsync(IncidentAffectedBooking caseRecord, DateTime now)
        {
            var booking = caseRecord.Booking;
            
            // Refund Money
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

            // Refund Points
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

            // Restore Voucher
            if (booking.AppliedVoucherId.HasValue)
            {
                var uv = await _context.UserVouchers.FirstOrDefaultAsync(v => v.UserId == booking.UserId && v.VoucherId == booking.AppliedVoucherId && v.IsUsed);
                if (uv != null)
                {
                    uv.IsUsed = false;
                    uv.UsedDate = null;
                }
                _context.IncidentFinancialOperations.Add(new IncidentFinancialOperation
                {
                    CaseId = caseRecord.Id,
                    Kind = "RestoreOriginalVoucher",
                    ExternalReference = booking.AppliedVoucherId.ToString(),
                    CreatedAtVn = now
                });
            }

            booking.Status = "CancelledBySystem";
            booking.UpdatedAt = now;

            caseRecord.Status = "Cancelled";
            caseRecord.Decision = "Cancel";
            caseRecord.DecidedAtVn = now;
        }

        private async Task HandleTransferDecisionAsync(int userId, IncidentAffectedBooking caseRecord, int targetBranchId, int targetSlotId, DateTime now)
        {
            var originalBooking = caseRecord.Booking;
            
            // Validate new slot
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
                throw new BadRequestException($"Khung giờ tại chi nhánh mới đã đầy hoặc đang có sự cố (còn lại: {cap.AvailableWeight}, cần: {ctx.CapacityWeight}).");
            }

            originalBooking.BranchId = targetBranchId;
            // No TimeSlotId in Booking
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
            // Find or create the 20% system voucher
            var sysVoucherCode = "INCIDENT_COMP_20";
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == sysVoucherCode);
            if (voucher == null)
            {
                voucher = new Voucher
                {
                    Code = sysVoucherCode,
                    DiscountAmount = 0, // Using percentage
                    DiscountPercent = 20,
                    IsActive = true,
                    StartDate = now.Date,
                    ExpiryDate = now.AddYears(10) // essentially no end date for the definition
                };
                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
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

        public async Task<IncidentAffectedBookingMobileDTO> GetAffectedBookingDetailsAsync(int userId, int bookingId)
        {
            var caseRecord = await _context.IncidentAffectedBookings
                .Include(c => c.Booking)
                .ThenInclude(b => b.Vehicle)
                .Include(c => c.Booking)
                .ThenInclude(b => b.BookingDetails)
                .FirstOrDefaultAsync(c => c.BookingId == bookingId && c.UserId == userId);

            if (caseRecord == null)
                throw new NotFoundException("Affected booking record not found.");

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == caseRecord.OriginalBranchId);

            var result = new IncidentAffectedBookingMobileDTO
            {
                AffectedBookingId = caseRecord.Id,
                BookingId = caseRecord.BookingId,
                Status = caseRecord.Status,
                CustomerDeadlineVn = caseRecord.ResponseDeadlineAtVn,
                BranchName = branch?.Name ?? "",
                ScheduledTime = caseRecord.OriginalScheduledTimeVn.ToString("yyyy-MM-dd HH:mm"),
                LicensePlate = caseRecord.Booking?.Vehicle?.LicensePlate ?? ""
            };

            if (caseRecord.Status == "AwaitingCustomer")
            {
                var originalBranchId = caseRecord.OriginalBranchId;
                var targetDate = caseRecord.OriginalScheduledTimeVn.Date;
                var vehicleTypeId = caseRecord.Booking?.Vehicle?.VehicleTypeId ?? 1;
                var serviceIds = caseRecord.Booking?.BookingDetails.Select(d => d.ServiceId).ToList() ?? new System.Collections.Generic.List<int>();

                var otherBranches = await _context.Branches.Where(b => b.BranchId != originalBranchId && b.IsActive).ToListAsync();

                foreach (var b in otherBranches)
                {
                    try
                    {
                        var slotsReq = new CheckAvailableSlotsRequestDTO
                        {
                            BranchId = b.BranchId,
                            TargetDate = targetDate,
                            VehicleTypeId = vehicleTypeId,
                            ServiceIds = serviceIds
                        };
                        var slots = await _bookingService.GetAvailableSlotsAsync(userId, slotsReq);
                        if (slots != null && slots.Count > 0)
                        {
                            var altOpt = new AlternativeBranchSlotDTO
                            {
                                BranchId = b.BranchId,
                                BranchName = b.Name,
                                DistanceKm = 0, // distance logic omitted
                                Slots = slots.Select(s => new AvailableSlotDTO { SlotId = s.SlotId, Time = s.TimeRange }).ToList()
                            };
                            result.AlternativeOptions.Add(altOpt);
                        }
                    }
                    catch
                    {
                        // ignore errors from single branch
                    }
                }
            }

            return result;
        }
    }
}
