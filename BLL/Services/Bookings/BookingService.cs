#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Helpers;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace AutoWashPro.BLL.Services
{
    public class BookingService : IBookingService
    {
        private readonly AutoWashDbContext _context;
        private readonly IWalletService _walletService;
        private readonly ITierService _tierService;
        private readonly IEmailService _emailService;
        private readonly IVoucherService _voucherService;
        private readonly IVoucherCampaignService _voucherCampaignService;
        private readonly IPayOsService _payOsService;
        private readonly IBookingMaterialUsageService _bookingMaterialUsageService;
        private readonly IOccupancyService _occupancyService;
        private readonly global::BLL.Services.Interface.ILaneSchedulerService _laneSchedulerService;
        private readonly AutoWashPro.BLL.Services.Operations.ILaneAdmissionCoordinator _laneCoordinator;
        private readonly global::BLL.Services.Interface.IPhotoService _photoService;
        private readonly IUserNotificationService _userNotificationService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            AutoWashDbContext context,
            IWalletService walletService,
            ITierService tierService,
            IEmailService emailService,
            IVoucherService voucherService,
            IVoucherCampaignService voucherCampaignService,
            IPayOsService payOsService,
            IBookingMaterialUsageService bookingMaterialUsageService,
            IOccupancyService occupancyService,
            global::BLL.Services.Interface.ILaneSchedulerService laneSchedulerService,
            AutoWashPro.BLL.Services.Operations.ILaneAdmissionCoordinator laneCoordinator,
            global::BLL.Services.Interface.IPhotoService photoService,
            IUserNotificationService userNotificationService,
            ILogger<BookingService> logger)
        {
            _context = context;
            _walletService = walletService;
            _tierService = tierService;
            _emailService = emailService;
            _voucherService = voucherService;
            _voucherCampaignService = voucherCampaignService;
            _payOsService = payOsService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
            _occupancyService = occupancyService;
            _laneSchedulerService = laneSchedulerService;
            _laneCoordinator = laneCoordinator;
            _photoService = photoService;
            _userNotificationService = userNotificationService;
            _logger = logger;
        }
        public async Task<List<TimeSlotResponseDTO>> GetAvailableSlotsAsync(int userId, CheckAvailableSlotsRequestDTO request)
        {
            var userProfile = await _context.CustomerProfiles.Include(cp => cp.Tier).FirstOrDefaultAsync(cp => cp.UserId == userId);
            if (userProfile == null || userProfile.Tier == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Membership tier information not found.");
            TimeZoneInfo vnTimeZone;
            try { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch (TimeZoneNotFoundException) { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            DateTime currentDateTimeInVN = TimeZoneInfo.ConvertTimeFromUtc(AutoWashPro.DAL.Helpers.TimeHelper.VnNow, vnTimeZone);
            DateTime todayInVN = currentDateTimeInVN.Date;
            var maxDate = todayInVN.AddDays(userProfile.Tier.BookingWindowDays);
            if (request.TargetDate.Date < todayInVN || request.TargetDate.Date > maxDate)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Tier {userProfile.Tier.TierName} can only book between today and {maxDate:dd/MM/yyyy}.");
            }
            int totalRequestWeight = 0;
            if (request.ServiceIds != null && request.ServiceIds.Any())
            {
                var vehicleType = await _context.VehicleTypes.FirstOrDefaultAsync(vt => vt.Id == request.VehicleTypeId);
                var baseWeight = vehicleType?.BaseWeight ?? 0;
                // FIX #2: Batch load all ServicePrices in 1 query instead of N queries
                var servicePricesBatch = await _context.ServicePrices
                    .Where(sp => request.ServiceIds.Contains(sp.ServiceId)
                              && sp.VehicleTypeId == request.VehicleTypeId
                              && sp.BranchId == request.BranchId)
                    .AsNoTracking()
                    .ToListAsync();
                foreach (var serviceId in request.ServiceIds)
                {
                    var servicePrice = servicePricesBatch.FirstOrDefault(sp => sp.ServiceId == serviceId);
                    var actualWeight = servicePrice?.CapacityWeight > 0 ? servicePrice.CapacityWeight : baseWeight;
                    if (actualWeight > totalRequestWeight) totalRequestWeight = actualWeight;
                }
            }
            var allSlots = await _context.TimeSlots
                .Where(s => s.BranchId == request.BranchId)
                .OrderBy(s => s.StartTime).ToListAsync();
            var response = new List<TimeSlotResponseDTO>();
            var dailyCapacities = await _context.DailySlotCapacities
                .Where(dc => dc.BranchId == request.BranchId && dc.Date == request.TargetDate.Date)
                .ToDictionaryAsync(dc => dc.SlotId, dc => dc.BookedWeight);
            bool isVip = userProfile.TotalPoint >= 5000 || (userProfile.Tier != null && (string.Equals(userProfile.Tier.TierName, "Gold", StringComparison.OrdinalIgnoreCase) || string.Equals(userProfile.Tier.TierName, "Platinum", StringComparison.OrdinalIgnoreCase) || string.Equals(userProfile.Tier.TierName, "Diamond", StringComparison.OrdinalIgnoreCase)));
            foreach (var slot in allSlots)
            {
                var slotDto = new TimeSlotResponseDTO
                {
                    SlotId = slot.SlotId,
                    TimeRange = $"{slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm}",
                    IsAvailable = true,
                    Reason = "Available"
                };
                if (slot.IsVipOnly && !isVip)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = "VIP only";
                }
                if (GetSlotEndDateTime(request.TargetDate, slot.StartTime, slot.EndTime) <= currentDateTimeInVN)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = "Past time";
                }
                int bookedWeight = dailyCapacities.TryGetValue(slot.SlotId, out int weight) ? weight : 0;
                if (bookedWeight + totalRequestWeight > slot.MaxCapacity)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = totalRequestWeight > 0 ? "Insufficient capacity for your cart" : "Fully booked";
                }
                response.Add(slotDto);
            }
            return response;
        }
        private static DateTime GetSlotEndDateTime(DateTime targetDate, TimeSpan startTime, TimeSpan endTime)
        {
            var slotStartDateTime = targetDate.Date.Add(startTime);
            var slotEndDateTime = targetDate.Date.Add(endTime);

            // A slot whose end time is not after its start time crosses midnight.
            if (slotEndDateTime <= slotStartDateTime)
            {
                slotEndDateTime = slotEndDateTime.AddDays(1);
            }

            return slotEndDateTime;
        }
        private double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; 
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return R * c;
        }
        public async Task<CheckSlotsWithSuggestionResponseDTO> GetAvailableSlotsWithSuggestionAsync(int userId, CheckAvailableSlotsRequestDTO request)
        {
            var slots = await GetAvailableSlotsAsync(userId, request);
            var currentBranch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == request.BranchId);
            if (currentBranch == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Branch not found.");
            double currentOccupancyRate = await _occupancyService.GetBranchOccupancyRateAsync(request.BranchId, request.TargetDate);
            bool isOverloaded = currentOccupancyRate >= 0.80 || !slots.Any(s => s.IsAvailable);
            var response = new CheckSlotsWithSuggestionResponseDTO
            {
                CurrentBranchId = currentBranch.BranchId,
                CurrentBranchName = currentBranch.Name,
                CurrentOccupancyRate = Math.Round(currentOccupancyRate, 2),
                IsOverloaded = isOverloaded,
                TimeSlots = slots,
                HasAlternativeSuggestion = false
            };
            if (!isOverloaded)
            {
                response.StatusMessage = "Chi nhÃ¡nh Ä‘ang cÃ³ sáºµn lá»‹ch trá»‘ng vÃ  cÃ´ng suáº¥t phá»¥c vá»¥ tá»‘t.";
                return response;
            }
            response.StatusMessage = $"Chi nhÃ¡nh {currentBranch.Name} hiá»‡n Ä‘ang ráº¥t Ä‘Ã´ng ({currentOccupancyRate * 100:F0}% kÃ­n lá»‹ch). Thá»i gian chá» cÃ³ thá»ƒ kÃ©o dÃ i.";
            if (!currentBranch.Latitude.HasValue || !currentBranch.Longitude.HasValue)
            {
                return response;
            }
            var altBranches = await _context.Branches
                .Where(b => b.IsActive && b.BranchId != request.BranchId && b.Latitude != null && b.Longitude != null)
                .ToListAsync();

            var nearbyBranches = altBranches
                .Select(b => new { 
                    Branch = b, 
                    Dist = CalculateHaversineDistanceKm(currentBranch.Latitude.Value, currentBranch.Longitude.Value, b.Latitude!.Value, b.Longitude!.Value) 
                })
                .Where(x => x.Dist <= 15.0)
                .ToList();

            var nearbyBranchIds = nearbyBranches.Select(x => x.Branch.BranchId).ToList();
            
            var allAltSlots = nearbyBranchIds.Count > 0
                ? await _context.TimeSlots.Where(s => nearbyBranchIds.Contains(s.BranchId)).ToListAsync()
                : new List<TimeSlot>();
                
            var allAltDailyCaps = nearbyBranchIds.Count > 0
                ? await _context.DailySlotCapacities.Where(dc => nearbyBranchIds.Contains(dc.BranchId) && dc.Date == request.TargetDate.Date).ToListAsync()
                : new List<DailySlotCapacity>();

            var candidates = new List<(Branch Branch, double DistanceKm, double OccupancyRate, int AvailableCount)>();
            foreach (var nearby in nearbyBranches)
            {
                var alt = nearby.Branch;
                double dist = nearby.Dist;
                
                double altOcc = await _occupancyService.GetBranchOccupancyRateAsync(alt.BranchId, request.TargetDate);
                var altSlots = allAltSlots.Where(s => s.BranchId == alt.BranchId).ToList();
                var dailyCaps = allAltDailyCaps.Where(dc => dc.BranchId == alt.BranchId).ToDictionary(dc => dc.SlotId, dc => dc.BookedWeight);
                int availCount = 0;
                foreach (var s in altSlots)
                {
                    int booked = dailyCaps.TryGetValue(s.SlotId, out int w) ? w : 0;
                    if (booked < s.MaxCapacity) availCount++;
                }
                if (availCount > 0 && altOcc < 0.70)
                {
                    candidates.Add((alt, dist, altOcc, availCount));
                }
            }
            var bestAlt = candidates
                .OrderBy(c => c.OccupancyRate)
                .ThenBy(c => c.DistanceKm)
                .FirstOrDefault();
            if (bestAlt.Branch != null)
            {
                string voucherCode = $"SWITCH_BR{bestAlt.Branch.BranchId}_15%";
                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == voucherCode);
                if (voucher == null)
                {
                    voucher = new Voucher
                    {
                        Code = voucherCode,
                        DiscountAmount = 15,
                        VoucherType = AutoWashPro.DAL.Enums.VoucherType.Discount,
                        CampaignType = AutoWashPro.DAL.Enums.VoucherCampaignType.Winback,
                        BranchId = bestAlt.Branch.BranchId,
                        ExpiryDays = 1,
                        MaxUsagePerUser = 5,
                        MaxUsages = 999999,
                        IsActive = true,
                        StartDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                        ExpiryDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddYears(1)
                    };
                    _context.Vouchers.Add(voucher);
                    await _context.SaveChangesAsync();
                }
                if (userId > 0)
                {
                    bool hasUserVoucher = await _context.UserVouchers.AnyAsync(uv => uv.UserId == userId && uv.VoucherId == voucher.VoucherId && uv.ExpiryDate >= AutoWashPro.DAL.Helpers.TimeHelper.VnNow && !uv.IsUsed);
                    if (!hasUserVoucher)
                    {
                        _context.UserVouchers.Add(new UserVoucher
                        {
                            UserId = userId,
                            VoucherId = voucher.VoucherId,
                            ReceivedDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                            ExpiryDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddDays(1),
                            IsUsed = false,
                            TriggerKey = $"SwitchBranch_BR{bestAlt.Branch.BranchId}"
                        });
                        await _context.SaveChangesAsync();
                    }
                }
                response.HasAlternativeSuggestion = true;
                response.SuggestedAlternative = new SuggestedBranchInfoDTO
                {
                    BranchId = bestAlt.Branch.BranchId,
                    BranchName = bestAlt.Branch.Name,
                    Address = bestAlt.Branch.Address,
                    DistanceKm = Math.Round(bestAlt.DistanceKm, 1),
                    OccupancyRate = Math.Round(bestAlt.OccupancyRate, 2),
                    AvailableSlotsCount = bestAlt.AvailableCount
                };
                response.IncentiveVoucher = new SwitchBranchIncentiveVoucherDTO
                {
                    VoucherId = voucher.VoucherId,
                    VoucherCode = voucher.Code,
                    DiscountPercentage = 15,
                    Description = $"ðŸŽ Táº·ng ngay MÃ£ giáº£m giÃ¡ 15% khi báº¡n Ä‘áº·t lá»‹ch sang {bestAlt.Branch.Name} hÃ´m nay!",
                    ExpiresInHours = 24
                };
            }
            return response;
        }
        public async Task<List<AdminBookingResponseDTO>> GetAllBookingsByDateAsync(DateTime targetDate)
        {
            var bookings = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingMaterialUsages).ThenInclude(u => u.Material)
                .Where(b => b.ScheduledTime.Date == targetDate.Date)
                .OrderBy(b => b.ScheduledTime)
                .Select(b => new AdminBookingResponseDTO
                {
                    BookingId = b.BookingId,
                    UserId = b.UserId,
                    LicensePlate = b.LicensePlate ?? "",
                    CustomerName = b.BusinessProfile != null
                        ? b.BusinessProfile.CompanyName
                        : b.User != null && b.User.CustomerProfile != null
                            ? b.User.CustomerProfile.FullName
                            : null,
                    CustomerPhone = b.User != null ? b.User.PhoneNumber : null,
                    BookingType = b.BookingType,
                    BusinessProfileId = b.BusinessProfileId,
                    FleetVehicleId = b.FleetVehicleId,
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointsUsed = b.PointsUsed,
                    PointDiscountAmount = b.PointDiscountAmount,
                    AppliedVoucherId = b.AppliedVoucherId,
                    AppliedVoucherCode = b.AppliedVoucher != null ? b.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount,
                    ProcessingStartTime = b.ProcessingStartTime,
                    CompletedTime = b.CompletedTime,
                    ActualDurationMinutes = b.ActualDurationMinutes,
                    ProcessingLaneId = b.ProcessingLaneId,
                    ProcessingLaneName = b.ProcessingLane != null ? b.ProcessingLane.Name : null,
                    CheckInImageUrl = b.CheckInImageUrl,
                    CheckOutImageUrl = b.CheckOutImageUrl,
                    MaterialUsages = b.BookingMaterialUsages.Select(u => new BookingMaterialUsageDTO
                    {
                        MaterialId = u.MaterialId,
                        MaterialName = u.Material.Name,
                        QuantityUsed = u.QuantityUsed,
                        Unit = u.Material.Unit,
                        CostAmount = u.CostAmount,
                        UsageType = u.UsageType
                    }).ToList()
                })
                .ToListAsync();
            var adminBookingIds = bookings.Select(b => b.BookingId).ToList();
            var adminPaymentTxs = await _context.Transactions
                .Where(t => t.ReferenceBookingId.HasValue
                    && adminBookingIds.Contains(t.ReferenceBookingId.Value)
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
                    t.TransactionType
                })
                .ToListAsync();

            var latestAdminTxByBooking = adminPaymentTxs
                .GroupBy(t => t.BookingId)
                .ToDictionary(g => g.Key, g => new
                {
                    Tx = g.FirstOrDefault(x => x.TransactionType != "Refund"),
                    IsRefunded = g.Any(x => x.TransactionType == "Refund")
                });

            foreach (var booking in bookings)
            {
                if (latestAdminTxByBooking.TryGetValue(booking.BookingId, out var paymentData))
                {
                    if (paymentData.IsRefunded)
                    {
                        booking.PaymentStatus = "Refunded";
                        booking.PaymentMethod = paymentData.Tx?.PaymentMethod;
                    }
                    else if (paymentData.Tx != null)
                    {
                        booking.PaymentStatus = paymentData.Tx.Status?.ToLowerInvariant() switch
                        {
                            "completed" => "Completed",
                            "pending"   => "Pending",
                            "expired"   => "Expired",
                            "failed"    => "Failed",
                            "cancelled" => "Failed",
                            _           => "Unpaid"
                        };
                        booking.PaymentMethod = paymentData.Tx.PaymentMethod;
                    }
                    else
                    {
                        booking.PaymentStatus = "Unpaid";
                        booking.PaymentMethod = null;
                    }
                }
                else
                {
                    booking.PaymentStatus = "Unpaid";
                    booking.PaymentMethod = null;
                }
                if (booking.ProcessingStartTime.HasValue)
                {
                    booking.ProcessingStartTime = booking.ProcessingStartTime.Value;
                }
                if (booking.CompletedTime.HasValue)
                {
                    booking.CompletedTime = booking.CompletedTime.Value;
                }
            }
            return bookings;
        }
        public async Task<BookingResponseDTO> GetBookingByIdAsync(int userId, int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
            if (booking == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking details not found or you do not have permission to view.");
            var txList = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.ReferenceBookingId == bookingId
                    && (t.TransactionType == "BookingPayment"
                        || t.TransactionType == "WalkInPayment"
                        || t.TransactionType == "Payment"
                        || t.TransactionType == "Refund"))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Status, t.PaymentMethod, t.TransactionType })
                .ToListAsync();

            var isRefunded = txList.Any(t => t.TransactionType == "Refund");
            var tx = txList.FirstOrDefault(t => t.TransactionType != "Refund");

            var paymentStatus = isRefunded
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
            return new BookingResponseDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = booking.LicensePlate,
                ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                ScheduledTime = booking.ScheduledTime,
                Status = booking.Status,
                OriginalPrice = booking.OriginalPrice,
                PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                FinalAmount = booking.FinalAmount,
                ProcessingStartTime = booking.ProcessingStartTime.HasValue ? booking.ProcessingStartTime.Value : (DateTime?)null,
                CompletedTime = booking.CompletedTime.HasValue ? booking.CompletedTime.Value : (DateTime?)null,
                ActualDurationMinutes = booking.ActualDurationMinutes,
                CheckInImageUrl = booking.CheckInImageUrl,
                CheckOutImageUrl = booking.CheckOutImageUrl,
                PaymentStatus = paymentStatus,
                PaymentMethod = tx?.PaymentMethod
            };
        }
        private string NormalizeLicensePlate(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return string.Empty;
            return new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpper();
        }
        public async Task<SmartLicensePlateResponseDTO> LookupLicensePlateAsync(string licensePlate, int branchId)
        {
            var normalizedPlate = NormalizeLicensePlate(licensePlate);
            if (string.IsNullOrEmpty(normalizedPlate))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid license plate.");
            var todayInVN = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.Date;
            var activeBookings = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingMaterialUsages).ThenInclude(u => u.Material)
                .Where(b => b.LicensePlate == normalizedPlate
                         && b.BranchId == branchId
                         && (b.Status == "CheckedIn" || b.Status == "Processing"))
                .Select(b => new AdminBookingResponseDTO
                {
                    BookingId = b.BookingId,
                    UserId = b.UserId,
                    LicensePlate = b.LicensePlate ?? "",
                    CustomerName = b.BusinessProfile != null
                        ? b.BusinessProfile.CompanyName
                        : b.User != null && b.User.CustomerProfile != null
                            ? b.User.CustomerProfile.FullName
                            : null,
                    CustomerPhone = b.User != null ? b.User.PhoneNumber : null,
                    BookingType = b.BookingType,
                    BusinessProfileId = b.BusinessProfileId,
                    FleetVehicleId = b.FleetVehicleId,
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointsUsed = b.PointsUsed,
                    PointDiscountAmount = b.PointDiscountAmount,
                    AppliedVoucherId = b.AppliedVoucherId,
                    AppliedVoucherCode = b.AppliedVoucher != null ? b.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount,
                    ProcessingStartTime = b.ProcessingStartTime,
                    CompletedTime = b.CompletedTime,
                    ActualDurationMinutes = b.ActualDurationMinutes,
                    ProcessingLaneId = b.ProcessingLaneId,
                    ProcessingLaneName = b.ProcessingLane != null ? b.ProcessingLane.Name : null,
                    MaterialUsages = b.BookingMaterialUsages.Select(u => new BookingMaterialUsageDTO
                    {
                        MaterialId = u.MaterialId,
                        MaterialName = u.Material.Name,
                        QuantityUsed = u.QuantityUsed,
                        Unit = u.Material.Unit,
                        CostAmount = u.CostAmount,
                        UsageType = u.UsageType
                    }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();
            var activeBooking = activeBookings
                .OrderByDescending(b => b.Status == "Processing")
                .ThenByDescending(b => b.ScheduledTime)
                .FirstOrDefault();
            if (activeBooking != null)
            {
                var isBusinessBooking = IsBusinessBooking(activeBooking);
                if (isBusinessBooking)
                {
                    activeBooking.PaymentStatus = "Completed";
                    activeBooking.PaymentMethod = "Business account";
                }
                else
                {
                    var paymentStatuses = await GetPaymentStatusesByBookingIdsAsync(new List<int> { activeBooking.BookingId });
                    activeBooking.PaymentStatus = GetPaymentStatus(paymentStatuses, activeBooking.BookingId);
                }
                if (activeBooking.ProcessingStartTime.HasValue)
                    activeBooking.ProcessingStartTime = activeBooking.ProcessingStartTime.Value;
                if (activeBooking.CompletedTime.HasValue)
                    activeBooking.CompletedTime = activeBooking.CompletedTime.Value;
                var vipInfo = await GetVipInfoByBookingIdAsync(activeBooking.BookingId);
                return new SmartLicensePlateResponseDTO
                {
                    CustomerType = "PreBooked",
                    Data = activeBooking,
                    CustomerTierName = isBusinessBooking ? "Business account" : vipInfo.TierName,
                    CustomerTierPoints = isBusinessBooking ? 0 : vipInfo.TotalPoint,
                    IsVip = !isBusinessBooking && vipInfo.IsVip
                };
            }
            var preBookedCandidates = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Where(b => b.LicensePlate == normalizedPlate
                         && b.BranchId == branchId
                         && (b.Status == "Pending" || b.Status == "Confirmed"))
                .OrderBy(b => b.ScheduledTime)
                .Select(b => new AdminBookingResponseDTO
                {
                    BookingId = b.BookingId,
                    UserId = b.UserId,
                    LicensePlate = b.LicensePlate ?? "",
                    CustomerName = b.BusinessProfile != null
                        ? b.BusinessProfile.CompanyName
                        : b.User != null && b.User.CustomerProfile != null
                            ? b.User.CustomerProfile.FullName
                            : null,
                    CustomerPhone = b.User != null ? b.User.PhoneNumber : null,
                    BookingType = b.BookingType,
                    BusinessProfileId = b.BusinessProfileId,
                    FleetVehicleId = b.FleetVehicleId,
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointsUsed = b.PointsUsed,
                    PointDiscountAmount = b.PointDiscountAmount,
                    AppliedVoucherId = b.AppliedVoucherId,
                    AppliedVoucherCode = b.AppliedVoucher != null ? b.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount,
                    ProcessingStartTime = b.ProcessingStartTime,
                    CompletedTime = b.CompletedTime,
                    ActualDurationMinutes = b.ActualDurationMinutes,
                    ProcessingLaneId = b.ProcessingLaneId,
                    ProcessingLaneName = b.ProcessingLane != null ? b.ProcessingLane.Name : null
                })
                .AsNoTracking()
                .ToListAsync();
            var preBooked = preBookedCandidates
                .OrderBy(b => b.ScheduledTime.Date == todayInVN ? 0 : 1)
                .ThenBy(b => Math.Abs((b.ScheduledTime - AutoWashPro.DAL.Helpers.TimeHelper.VnNow).Ticks))
                .FirstOrDefault();
            if (preBooked != null)
            {
                var isBusinessBooking = IsBusinessBooking(preBooked);
                if (isBusinessBooking)
                {
                    preBooked.PaymentStatus = "Completed";
                    preBooked.PaymentMethod = "Business account";
                }
                else
                {
                    var paymentStatuses = await GetPaymentStatusesByBookingIdsAsync(new List<int> { preBooked.BookingId });
                    preBooked.PaymentStatus = GetPaymentStatus(paymentStatuses, preBooked.BookingId);
                }
                if (preBooked.ProcessingStartTime.HasValue)
                    preBooked.ProcessingStartTime = preBooked.ProcessingStartTime.Value;
                if (preBooked.CompletedTime.HasValue)
                    preBooked.CompletedTime = preBooked.CompletedTime.Value;
                var vipInfo = await GetVipInfoByBookingIdAsync(preBooked.BookingId);
                return new SmartLicensePlateResponseDTO
                {
                    CustomerType = "PreBooked",
                    Data = preBooked,
                    CustomerTierName = isBusinessBooking ? "Business account" : vipInfo.TierName,
                    CustomerTierPoints = isBusinessBooking ? 0 : vipInfo.TotalPoint,
                    IsVip = !isBusinessBooking && vipInfo.IsVip
                };
            }
            var fleetVehicle = await _context.FleetVehicles
                .Include(fv => fv.BusinessProfile)
                .Include(fv => fv.VehicleType)
                .Where(fv => fv.LicensePlate == normalizedPlate
                          && (fv.Status == "Approved" || fv.Status == "Active")
                          && fv.BusinessProfile != null
                          && fv.BusinessProfile.ApprovalStatus == "Approved"
                          && fv.BusinessProfile.IsContractActive)
                .Select(fv => new global::BLL.DTOs.Fleet.FleetVehicleDTO
                {
                    FleetVehicleId = fv.FleetVehicleId,
                    BusinessProfileId = fv.BusinessProfileId,
                    CompanyName = fv.BusinessProfile!.CompanyName,
                    LicensePlate = fv.LicensePlate,
                    VehicleType = fv.VehicleType.Name,
                    VehicleTypeName = fv.VehicleType.Name,
                    Brand = fv.Brand,
                    Model = fv.Model,
                    DriverName = fv.DriverName,
                    EmployeeId = fv.EmployeeCode,
                    Status = fv.Status,
                    CreatedAt = fv.CreatedAt
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (fleetVehicle != null)
            {
                return new SmartLicensePlateResponseDTO
                {
                    CustomerType = "Fleet",
                    Data = fleetVehicle
                };
            }
            var registeredVehicle = await _context.Vehicles
                .Include(v => v.User)
                .ThenInclude(u => u.CustomerProfile)
                    .ThenInclude(cp => cp.Tier)
                .Where(v => v.LicensePlate == normalizedPlate && !v.IsDeleted && v.UserId != null)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (registeredVehicle != null && registeredVehicle.User != null)
            {
                var profile = registeredVehicle.User.CustomerProfile;
                var tierName = profile?.Tier?.TierName;
                var totalPoint = profile?.TotalPoint ?? 0;
                var isVip = totalPoint >= 5000 ||
                            tierName == "Gold" || tierName == "Platinum" || tierName == "Diamond";
                return new SmartLicensePlateResponseDTO
                {
                    CustomerType = "WalkIn",
                    Data = new
                    {
                        userId = registeredVehicle.UserId,
                        customerName = profile?.FullName,
                        phoneNumber = registeredVehicle.User.PhoneNumber,
                        vehicleId = registeredVehicle.Id
                    },
                    CustomerTierName = tierName,
                    CustomerTierPoints = totalPoint,
                    IsVip = isVip
                };
            }
            return new SmartLicensePlateResponseDTO
            {
                CustomerType = "WalkIn",
                Data = null
            };
        }
        private static bool IsBusinessBooking(AdminBookingResponseDTO booking)
        {
            return booking.BusinessProfileId.HasValue ||
                   string.Equals(booking.BookingType, "Business", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(booking.BookingType, "Fleet", StringComparison.OrdinalIgnoreCase);
        }
        private async Task<(string? TierName, int TotalPoint, bool IsVip)> GetVipInfoByBookingIdAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Where(b => b.BookingId == bookingId)
                .Select(b => new { b.UserId })
                .FirstOrDefaultAsync();
            if (booking?.UserId == null)
                return (null, 0, false);
            var profile = await _context.CustomerProfiles
                .Include(cp => cp.Tier)
                .Where(cp => cp.UserId == booking.UserId)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (profile == null)
                return (null, 0, false);
            var tierName = profile.Tier?.TierName;
            var totalPoint = profile.TotalPoint;
            var isVip = totalPoint >= 5000 ||
                        tierName == "Gold" || tierName == "Platinum" || tierName == "Diamond";
            return (tierName, totalPoint, isVip);
        }
        private async Task<Dictionary<int, string>> GetPaymentStatusesByBookingIdsAsync(IEnumerable<int> bookingIds)
        {
            var ids = bookingIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }
            var paymentTransactions = await _context.Transactions
                .Where(t => t.ReferenceBookingId.HasValue
                    && ids.Contains(t.ReferenceBookingId.Value)
                    && (EF.Property<string?>(t, nameof(Transaction.TransactionType)) == "Payment"
                        || EF.Property<string?>(t, nameof(Transaction.TransactionType)) == "BookingPayment"
                        || EF.Property<string?>(t, nameof(Transaction.TransactionType)) == "WalkInPayment"
                        || EF.Property<string?>(t, nameof(Transaction.TransactionType)) == "Refund"))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    BookingId = t.ReferenceBookingId!.Value,
                    Status = EF.Property<string?>(t, nameof(Transaction.Status)) ?? "",
                    TransactionType = EF.Property<string?>(t, nameof(Transaction.TransactionType)) ?? "",
                    PaymentMethod = EF.Property<string?>(t, nameof(Transaction.PaymentMethod)),
                    Amount = t.Amount,
                    OrderCode = EF.Property<string?>(t, nameof(Transaction.OrderCode)),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();
            return paymentTransactions
                .GroupBy(t => t.BookingId)
                .ToDictionary(
                    g => g.Key, 
                    g => g.Any(x => x.TransactionType == "Refund") ? "Refunded" : g.FirstOrDefault(x => x.TransactionType != "Refund")?.Status ?? ""
                );
        }
        private static string GetPaymentStatus(Dictionary<int, string> paymentStatuses, int bookingId)
        {
            if (!paymentStatuses.TryGetValue(bookingId, out var paymentStatus)
                || string.IsNullOrWhiteSpace(paymentStatus))
            {
                return "Unpaid";
            }
            if (paymentStatus == "Refunded") return "Refunded";
            return paymentStatus.ToLowerInvariant() switch
            {
                "completed" => "Completed",
                "pending"   => "Pending",
                "expired"   => "Expired",
                "failed"    => "Failed",
                "cancelled" => "Failed",
                _           => "Unpaid"
            };
        }
        public async Task<BookingPaymentStatusDTO> GetBookingPaymentStatusAsync(int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.ProcessingLane)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Booking #{bookingId} not found.");
            var txList = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.ReferenceBookingId == bookingId
                    && (t.TransactionType == "BookingPayment"
                        || t.TransactionType == "WalkInPayment"
                        || t.TransactionType == "Payment"
                        || t.TransactionType == "Refund"))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var isRefunded = txList.Any(t => t.TransactionType == "Refund");
            var tx = txList.FirstOrDefault(t => t.TransactionType != "Refund");

            string paymentStatus;
            string? paymentMethod = tx?.PaymentMethod;
            string? orderCode = tx?.OrderCode;
            decimal? amount = tx?.Amount;
            DateTime? paidAt = null;
            
            if (isRefunded)
            {
                paymentStatus = "Refunded";
            }
            else if (tx == null)
            {
                paymentStatus = "Unpaid";
            }
            else if (string.Equals(tx.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                paymentStatus = "Completed";
            }
            else if (string.Equals(tx.Status, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                paymentStatus = "Expired";
            }
            else if (string.Equals(tx.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                paymentStatus = "Failed";
            }
            else
            {
                paymentStatus = "Pending";
            }
            if (paymentStatus != "Completed" && string.Equals(tx?.PaymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(tx?.OrderCode))
            {
                try
                {
                    var verified = await _payOsService.GetPaymentStatusAsync(tx.OrderCode!);
                    if (verified != null && verified.IsPaid)
                    {
                        await _walletService.ConfirmTransactionPaymentAsync(tx.TransactionId, verified.Amount, tx.OrderCode!);
                        paymentStatus = "Completed";
                        paidAt = verified.PaidAt ?? AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                        booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                            .Include(b => b.ProcessingLane)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
                    }
                    else if (verified != null && verified.IsCancelled)
                    {
                        var terminalStatus = string.Equals(verified.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase)
                            ? "Expired"
                            : "Failed";
                        paymentStatus = terminalStatus;
                        await _walletService.MarkTransactionTerminalAsync(tx.TransactionId, terminalStatus);
                    }
                }
                catch (Exception)
                {
                }
            }
            if (paymentStatus == "Completed" && tx != null && string.Equals(tx.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                paidAt = tx.CreatedAt;
            }
            return new BookingPaymentStatusDTO
            {
                BookingId = bookingId,
                PaymentStatus = paymentStatus,
                PaymentMethod = paymentMethod,
                OrderCode = orderCode,
                Amount = amount,
                PaidAt = paidAt,
                ProcessingLaneId = booking.ProcessingLaneId,
                ProcessingLaneName = booking.ProcessingLane?.Name
            };
        }
        public async Task<BookingResponseDTO> UpdateBookingStatusByLicensePlateAsync(string licensePlate, string newStatus, IFormFile? checkInImage = null, bool allowOutsideScheduledTime = false)
        {
            if (!BookingStatuses.IsAllowed(newStatus))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid status.");

            var normalizedPlate = NormalizeLicensePlate(licensePlate);
            if (string.IsNullOrEmpty(normalizedPlate))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid license plate.");

            var matches = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Include(b => b.ProcessingLane)
                .Where(b => b.LicensePlate != null && b.LicensePlate == normalizedPlate)
                .ToListAsync();
            if (matches.Count == 0)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"No recent booking found for vehicle {licensePlate}.");

            var todayInVN = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.Date;
            var candidates = matches
                .Where(b => b.ScheduledTime.Date == todayInVN)
                .ToList();
            var todaysBookings = candidates
                .Where(b => BookingStatuses.CanTransitionFrom(b.Status, newStatus))
                .ToList();

            if (todaysBookings.Count == 0)
            {
                if (newStatus == BookingStatuses.CheckedIn)
                {
                    var nearestBooking = matches
                        .Where(b => b.Status == BookingStatuses.Pending || b.Status == BookingStatuses.Confirmed)
                        .OrderBy(b => Math.Abs((b.ScheduledTime - AutoWashPro.DAL.Helpers.TimeHelper.VnNow).Ticks))
                        .FirstOrDefault();
                    if (nearestBooking != null)
                    {
                        await BookingCheckInPolicy.ValidateAsync(
                            _context,
                            nearestBooking,
                            allowOutsideScheduledTime);
                    }
                }
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Vehicle {licensePlate} has a booking, but it is not scheduled for today ({todayInVN:dd/MM/yyyy}).");
            }
            if (todaysBookings.Count > 1)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Multiple bookings ({todaysBookings.Count}) detected for vehicle {licensePlate} today. Please use the Booking ID to proceed.");

            var booking = todaysBookings.First();
            if (booking.Status == newStatus)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"The current status of the order is already '{newStatus}'.");

            if (newStatus == BookingStatuses.CheckedIn)
            {
                await BookingCheckInPolicy.ValidateAsync(
                    _context,
                    booking,
                    allowOutsideScheduledTime);
                if (checkInImage == null || checkInImage.Length == 0)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Check-in image is required.");
                booking.CheckInImageUrl = await _photoService.UploadImageAsync(checkInImage);
            }
            var (isUpdated, checkInResult, checkOutResult) = await UpdateBookingStatusAsync(
                booking.BookingId,
                newStatus,
                allowOutsideScheduledTime);
            if (!isUpdated)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Status update failed due to system error.");

            return new BookingResponseDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = normalizedPlate,
                ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                ScheduledTime = booking.ScheduledTime,
                Status = newStatus,
                OriginalPrice = booking.OriginalPrice,
                PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                FinalAmount = booking.FinalAmount,
                ProcessingStartTime = booking.ProcessingStartTime.HasValue ? booking.ProcessingStartTime.Value : (DateTime?)null,
                CompletedTime = booking.CompletedTime.HasValue ? booking.CompletedTime.Value : (DateTime?)null,
                ActualDurationMinutes = booking.ActualDurationMinutes,
                CheckInImageUrl = booking.CheckInImageUrl,
                CheckOutImageUrl = booking.CheckOutImageUrl,
                ProcessingLaneId = booking.ProcessingLaneId,
                ProcessingLaneName = booking.ProcessingLane?.Name,
                IsWaitingForLane = booking.ProcessingLaneId == null && newStatus == BookingStatuses.CheckedIn,
                BarrierCommandId = checkInResult?.BarrierCommandId,
                BarrierId = checkInResult?.BarrierId,
                BarrierCommandExpiresAt = checkInResult?.BarrierCommandExpiresAt
            };
        }
        public async Task<BookingResponseDTO> AutoCheckOutByLicensePlateAsync(string licensePlate, IFormFile checkOutImage)
        {
            if (checkOutImage == null || checkOutImage.Length == 0)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Check-out image is required.");
            var normalizedPlate = NormalizeLicensePlate(licensePlate);
            if (string.IsNullOrEmpty(normalizedPlate))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid license plate.");
            var activeBooking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                .Include(b => b.ProcessingLane)
                .Where(b => b.LicensePlate == normalizedPlate
                         && (b.Status == BookingStatuses.CheckedIn || b.Status == BookingStatuses.Processing))
                .OrderByDescending(b => b.BookingId)
                .FirstOrDefaultAsync();
            var activeFleetLog = await _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                .Include(x => x.Booking).ThenInclude(b => b!.BookingDetails).ThenInclude(bd => bd.Service)
                .Where(x => x.FleetVehicle.LicensePlate == normalizedPlate
                         && (x.Status == BookingStatuses.CheckedIn || x.Status == BookingStatuses.Processing || x.Status == "Assigned"))
                .OrderByDescending(x => x.FleetWashLogId)
                .FirstOrDefaultAsync();
            if (activeBooking == null && activeFleetLog == null)
            {
                var recentCompletion = await GetRecentCameraCheckOutAsync(normalizedPlate);
                if (recentCompletion != null)
                {
                    return recentCompletion;
                }

                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"No active wash session (CheckedIn/Processing) found for vehicle {licensePlate} to complete check-out.");
            }
            Booking? targetBooking = activeBooking ?? activeFleetLog?.Booking;
            if (targetBooking != null)
            {
                if (targetBooking.FinalAmount > 0 && !await HasCompletedBookingPaymentAsync(targetBooking.BookingId))
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Booking is unpaid; cannot check out barrier.");
                }
                var checkOutImageUrl = await _photoService.UploadImageAsync(checkOutImage);
                targetBooking.CheckOutImageUrl = checkOutImageUrl;
                if (activeFleetLog != null)
                {
                    activeFleetLog.CheckOutImageUrl = checkOutImageUrl;
                }
                var (isUpdated, _, checkOutResult) = await UpdateBookingStatusAsync(targetBooking.BookingId, BookingStatuses.Completed);
                if (activeFleetLog != null && activeFleetLog.Status != BookingStatuses.Completed)
                {
                    activeFleetLog.Status = BookingStatuses.Completed;
                    activeFleetLog.CompletedTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                    await _context.SaveChangesAsync();
                    if (activeFleetLog.LaneId > 0)
                    {
                        await _laneCoordinator.CheckOutAtExitGateAsync(normalizedPlate, activeFleetLog.BranchId);
                    }
                }
                var updatedBooking = await _context.Bookings.Include(b => b.AppliedVoucher)
                    .Include(b => b.BookingDetails).ThenInclude(bd => bd.Service)
                    .Include(b => b.ProcessingLane)
                    .FirstOrDefaultAsync(b => b.BookingId == targetBooking.BookingId) ?? targetBooking;
                return new BookingResponseDTO
                {
                    BookingId = updatedBooking.BookingId,
                    LicensePlate = normalizedPlate,
                    ServiceNames = updatedBooking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                    ScheduledTime = updatedBooking.ScheduledTime,
                    Status = BookingStatuses.Completed,
                    OriginalPrice = updatedBooking.OriginalPrice,
                    PointsUsed = updatedBooking.PointsUsed,
                    PointDiscountAmount = updatedBooking.PointDiscountAmount,
                    AppliedVoucherId = updatedBooking.AppliedVoucherId,
                    AppliedVoucherCode = updatedBooking.AppliedVoucher != null ? updatedBooking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = updatedBooking.VoucherDiscountAmount,
                    FinalAmount = updatedBooking.FinalAmount,
                    ProcessingStartTime = updatedBooking.ProcessingStartTime.HasValue ? updatedBooking.ProcessingStartTime.Value : (DateTime?)null,
                    CompletedTime = updatedBooking.CompletedTime.HasValue ? updatedBooking.CompletedTime.Value : AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                    ActualDurationMinutes = updatedBooking.ActualDurationMinutes,
                    CheckInImageUrl = updatedBooking.CheckInImageUrl,
                    CheckOutImageUrl = updatedBooking.CheckOutImageUrl,
                    ProcessingLaneId = updatedBooking.ProcessingLaneId,
                    ProcessingLaneName = updatedBooking.ProcessingLane?.Name,
                    IsWaitingForLane = false,
                    ExitBarrierCommandId = checkOutResult?.ExitBarrierCommandId,
                    BarrierId = checkOutResult?.BarrierId,
                    BarrierCommandExpiresAt = checkOutResult?.BarrierCommandExpiresAt
                };
            }
            else if (activeFleetLog != null)
            {
                activeFleetLog.CheckOutImageUrl = await _photoService.UploadImageAsync(checkOutImage);
                activeFleetLog.Status = BookingStatuses.Completed;
                activeFleetLog.CompletedTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                await _context.SaveChangesAsync();
                AutoWashPro.BLL.Services.Operations.CheckOutResult? checkOutResult = null;
                if (activeFleetLog.LaneId > 0)
                {
                    checkOutResult = await _laneCoordinator.CheckOutAtExitGateAsync(normalizedPlate, activeFleetLog.BranchId);
                }
                return new BookingResponseDTO
                {
                    BookingId = activeFleetLog.FleetWashLogId,
                    LicensePlate = normalizedPlate,
                    ServiceNames = new List<string> { "Fleet Wash Service" },
                    ScheduledTime = activeFleetLog.CheckInTime,
                    Status = BookingStatuses.Completed,
                    OriginalPrice = activeFleetLog.WashCost,
                    PointDiscountAmount = 0,
                    VoucherDiscountAmount = 0,
                    FinalAmount = activeFleetLog.WashCost,
                    ProcessingStartTime = activeFleetLog.CheckInTime,
                    CompletedTime = activeFleetLog.CompletedTime.Value,
                    ActualDurationMinutes = (int)Math.Max(1, Math.Round((activeFleetLog.CompletedTime.Value - activeFleetLog.CheckInTime).TotalMinutes)),
                    CheckInImageUrl = activeFleetLog.CheckInImageUrl,
                    CheckOutImageUrl = activeFleetLog.CheckOutImageUrl,
                    ProcessingLaneId = activeFleetLog.LaneId,
                    ProcessingLaneName = null, 
                    IsWaitingForLane = false,
                    ExitBarrierCommandId = checkOutResult?.ExitBarrierCommandId,
                    BarrierId = checkOutResult?.BarrierId,
                    BarrierCommandExpiresAt = checkOutResult?.BarrierCommandExpiresAt
                };
            }
            throw new AutoWashPro.BLL.Exceptions.NotFoundException($"No active wash session found for vehicle {licensePlate}.");
        }

        private async Task<BookingResponseDTO?> GetRecentCameraCheckOutAsync(string normalizedPlate)
        {
            var cutoff = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddMinutes(-5);
            var recentBooking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .AsNoTracking()
                .Include(b => b.BookingDetails)
                    .ThenInclude(detail => detail.Service)
                .Where(b =>
                    (b.LicensePlate == normalizedPlate ||
                        (b.Vehicle != null && b.Vehicle.LicensePlate == normalizedPlate)) &&
                    b.Status == "Completed" &&
                    b.CompletedTime >= cutoff)
                .OrderByDescending(b => b.CompletedTime)
                .ThenByDescending(b => b.BookingId)
                .FirstOrDefaultAsync();

            if (recentBooking != null)
            {
                var command = await _context.BarrierCommands
                    .AsNoTracking()
                    .Where(c =>
                        c.BookingId == recentBooking.BookingId &&
                        c.BarrierId == "EXIT_GATE" &&
                        c.Action == "OPEN")
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                return new BookingResponseDTO
                {
                    BookingId = recentBooking.BookingId,
                    BranchId = recentBooking.BranchId,
                    LicensePlate = normalizedPlate,
                    ServiceNames = recentBooking.BookingDetails
                        .Select(detail => detail.Service.ServiceName)
                        .ToList(),
                    ScheduledTime = recentBooking.ScheduledTime,
                    Status = recentBooking.Status,
                    OriginalPrice = recentBooking.OriginalPrice,
                    PointsUsed = recentBooking.PointsUsed,
                    PointDiscountAmount = recentBooking.PointDiscountAmount,
                    AppliedVoucherId = recentBooking.AppliedVoucherId,
                    AppliedVoucherCode = recentBooking.AppliedVoucher != null ? recentBooking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = recentBooking.VoucherDiscountAmount,
                    FinalAmount = recentBooking.FinalAmount,
                    ProcessingStartTime = recentBooking.ProcessingStartTime,
                    CompletedTime = recentBooking.CompletedTime,
                    ActualDurationMinutes = recentBooking.ActualDurationMinutes,
                    CheckInImageUrl = recentBooking.CheckInImageUrl,
                    CheckOutImageUrl = recentBooking.CheckOutImageUrl,
                    IsWaitingForLane = false,
                    ExitBarrierCommandId = command?.CommandId,
                    BarrierId = command?.BarrierId,
                    BarrierCommandExpiresAt = command?.ExpiresAt,
                    BarrierCommandStatus = command?.Status,
                    IsDuplicate = true
                };
            }

            var recentFleetLog = await _context.FleetWashLogs
                .AsNoTracking()
                .Include(log => log.FleetVehicle)
                .Where(log =>
                    log.BookingId == null &&
                    log.FleetVehicle.LicensePlate == normalizedPlate &&
                    log.Status == "Completed" &&
                    log.CompletedTime >= cutoff)
                .OrderByDescending(log => log.CompletedTime)
                .ThenByDescending(log => log.FleetWashLogId)
                .FirstOrDefaultAsync();

            if (recentFleetLog == null)
            {
                return null;
            }

            var fleetCommand = await _context.BarrierCommands
                .AsNoTracking()
                .Where(c =>
                    c.FleetWashLogId == recentFleetLog.FleetWashLogId &&
                    c.BarrierId == "EXIT_GATE" &&
                    c.Action == "OPEN")
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            return new BookingResponseDTO
            {
                BookingId = recentFleetLog.FleetWashLogId,
                BranchId = recentFleetLog.BranchId,
                LicensePlate = normalizedPlate,
                ServiceNames = new List<string> { "Fleet Wash Service" },
                ScheduledTime = recentFleetLog.CheckInTime,
                Status = recentFleetLog.Status ?? "Completed",
                OriginalPrice = recentFleetLog.WashCost,
                FinalAmount = recentFleetLog.WashCost,
                ProcessingStartTime = recentFleetLog.CheckInTime,
                CompletedTime = recentFleetLog.CompletedTime,
                ActualDurationMinutes = recentFleetLog.CompletedTime.HasValue
                    ? (int)Math.Max(1, Math.Round((recentFleetLog.CompletedTime.Value - recentFleetLog.CheckInTime).TotalMinutes))
                    : null,
                CheckInImageUrl = recentFleetLog.CheckInImageUrl,
                CheckOutImageUrl = recentFleetLog.CheckOutImageUrl,
                IsWaitingForLane = false,
                ExitBarrierCommandId = fleetCommand?.CommandId,
                BarrierId = fleetCommand?.BarrierId,
                BarrierCommandExpiresAt = fleetCommand?.ExpiresAt,
                BarrierCommandStatus = fleetCommand?.Status,
                IsDuplicate = true
            };
        }

        public async Task<(bool Success, AutoWashPro.BLL.Services.Operations.GateCheckInResult? CheckInResult, AutoWashPro.BLL.Services.Operations.CheckOutResult? CheckOutResult)> UpdateBookingStatusAsync(int bookingId, string newStatus, bool allowOutsideScheduledTime = false)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found.");
            var allowedStatuses = new[] { "Pending", "CheckedIn", "Processing", "Completed", "Cancelled", "Delayed", "CancelledBySystem" };
            if (!allowedStatuses.Contains(newStatus)) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid status.");
            if (newStatus == "CheckedIn")
            {
                await BookingCheckInPolicy.ValidateAsync(
                    _context,
                    booking,
                    allowOutsideScheduledTime);
            }
            if ((newStatus == "CheckedIn" || newStatus == "Processing" || newStatus == "Completed")
                && !await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, booking))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("BOOKING_PAYMENT_REQUIRED", "BOOKING_PAYMENT_REQUIRED");
            if (newStatus == "Processing" && booking.ProcessingLaneId == null)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Booking does not have an assigned lane; cannot start processing.");
            }
            AutoWashPro.BLL.Services.Operations.GateCheckInResult? checkInResult = null;
            AutoWashPro.BLL.Services.Operations.CheckOutResult? checkOutResult = null;
            var isCompletingNow = false;
            if (newStatus == "CheckedIn" && booking.ProcessingLaneId == null)
            {
                checkInResult = await _laneCoordinator.CheckInAtEntryGateAsync(
                    booking.LicensePlate ?? "UNKNOWN",
                    booking.BranchId,
                    bookingId: booking.BookingId);
                await _context.Entry(booking).ReloadAsync();
            }
            else
            {
                isCompletingNow = newStatus == "Completed" && booking.Status != "Completed";
                if (isCompletingNow && !await _context.LaneOccupancies.AnyAsync(o => o.BookingId == booking.BookingId))
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException(
                        "The vehicle does not have an active lane occupancy; check-out was not completed.",
                        "LANE_OCCUPANCY_NOT_FOUND");
                }

                booking.Status = newStatus;
                booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                if (newStatus == "Processing" && booking.BusinessProfileId.HasValue)
                {
                    var fleetWashLog = await _context.FleetWashLogs
                        .FirstOrDefaultAsync(log =>
                            log.BookingId == booking.BookingId &&
                            log.Status != "Completed" &&
                            log.Status != "Cancelled");
                    if (fleetWashLog != null)
                    {
                        fleetWashLog.Status = "Processing";
                        fleetWashLog.LaneId = booking.ProcessingLaneId;
                    }
                }
                if (newStatus == "Completed")
                {
                    await _bookingMaterialUsageService.ConsumeForCompletedBookingAsync(booking.BookingId);
                }
                if (isCompletingNow)
                {
                    booking.CompletedTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                    if (booking.ProcessingStartTime.HasValue)
                    {
                        var duration = (int)Math.Round((booking.CompletedTime.Value - booking.ProcessingStartTime.Value).TotalMinutes);
                        booking.ActualDurationMinutes = duration < 1 ? 1 : duration;
                    }
                    if (booking.UserId > 0)
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
                }
            }

            if (isCompletingNow)
            {
                checkOutResult = await _laneCoordinator.CompletePhysicalCheckoutAsync(booking.BookingId, 0);
            }
            else if (newStatus == "Cancelled" || newStatus == "CancelledBySystem" || newStatus == "Delayed")
            {
                await _laneCoordinator.ReleaseLaneAsync(booking.BookingId, newStatus);
            }

            await _context.SaveChangesAsync();
            if (isCompletingNow && booking.UserId.HasValue)
            {
                await _voucherCampaignService.ProcessMilestoneCampaignsAsync(booking.UserId.Value);
            }
            return (true, checkInResult, checkOutResult);
        }
        public async Task<CompatibilityDTO> ValidateBookingCompatibilityAsync(int userId, int branchId, int slotId, DateTime targetDate, int? vehicleId, string licensePlate, List<int> serviceIds)
        {
            if (serviceIds == null || serviceIds.Count == 0)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Please select at least 1 service.");
            serviceIds = serviceIds.Distinct().ToList();
            var slot = await _context.TimeSlots.FirstOrDefaultAsync(ts => ts.SlotId == slotId && ts.BranchId == branchId);
            if (slot == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Invalid time slot.");
            if (slot.IsVipOnly)
            {
                var profile = await _context.CustomerProfiles
                    .Include(p => p.Tier)
                    .FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile == null || profile.Tier == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Membership tier information not found.");
                bool isVipTier = profile.TotalPoint >= 5000 || string.Equals(profile.Tier.TierName, "Gold", StringComparison.OrdinalIgnoreCase) || string.Equals(profile.Tier.TierName, "Platinum", StringComparison.OrdinalIgnoreCase) || string.Equals(profile.Tier.TierName, "Diamond", StringComparison.OrdinalIgnoreCase);
                if (!isVipTier)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("This time slot is exclusive to VIP members (Gold tier or above).");
                }
            }
            var targetDateTime = targetDate.Date.Add(slot.StartTime);
            if (targetDateTime <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Cannot book a time slot in the past or that has already started.");
            int totalCapacityWeight = 0;
            var vehicle = await _context.Vehicles.Include(v => v.VehicleType).FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && v.UserId == userId && !v.IsDeleted);
            if (vehicle == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Vehicle with license plate {licensePlate} does not exist in your profile.");
            bool hasActiveBooking = await _context.Bookings.Include(b => b.AppliedVoucher).AnyAsync(b => b.LicensePlate == licensePlate && (b.Status == "Pending" || b.Status == "CheckedIn"));
            if (hasActiveBooking)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Vehicle with license plate {licensePlate} has an unfinished booking. Cannot create a new booking.");
            foreach (var serviceId in serviceIds)
            {
                var service = await _context.Services.FindAsync(serviceId);
                if (service == null || !service.IsActive)
                    throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Service {serviceId} does not exist or has been discontinued.");
                var servicePrice = await _context.ServicePrices.FirstOrDefaultAsync(sp => sp.ServiceId == serviceId && sp.VehicleTypeId == vehicle.VehicleTypeId && sp.BranchId == branchId);
                if (servicePrice == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Service {serviceId} is not supported for this vehicle type.");
                var actualCapacityWeight = servicePrice.CapacityWeight > 0 ? servicePrice.CapacityWeight : vehicle.VehicleType.BaseWeight;
                if (actualCapacityWeight > totalCapacityWeight) totalCapacityWeight = actualCapacityWeight;
            }
            var dailyCapacity = await _context.DailySlotCapacities.FirstOrDefaultAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == branchId && dc.Date == targetDateTime.Date);
            int bookedWeight = dailyCapacity?.BookedWeight ?? 0;
            int remainingCapacity = slot.MaxCapacity - bookedWeight;
            if (bookedWeight + totalCapacityWeight > slot.MaxCapacity)
            {
                return new CompatibilityDTO
                {
                    IsCompatible = false,
                    Message = "Insufficient shop capacity for your request.",
                    RemainingCapacity = remainingCapacity > 0 ? remainingCapacity : 0,
                    TotalCapacityWeight = totalCapacityWeight,
                    MaxCapacityOfSlot = slot.MaxCapacity
                };
            }
            return new CompatibilityDTO
            {
                IsCompatible = true,
                Message = "Capacity available.",
                RemainingCapacity = remainingCapacity,
                TotalCapacityWeight = totalCapacityWeight,
                MaxCapacityOfSlot = slot.MaxCapacity
            };
        }
        public async Task<bool> SendBookingConfirmationEmailAsync(int userId, int bookingId)
        {
            try
            {
                var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                    .Include(b => b.BookingDetails)
                        .ThenInclude(bd => bd.Service)
                    .Include(b => b.User)
                        .ThenInclude(u => u.CustomerProfile)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
                if (booking == null || string.IsNullOrEmpty(booking.User?.Email))
                {
                    Console.WriteLine($"[Warning] Cannot send mail: Order #{bookingId} not found or User has no email.");
                    return false;
                }
                var customerName = booking.User.CustomerProfile?.FullName ?? "Valued Customer";
                var emailHtml = EmailTemplateBuilder.BuildBookingConfirmationEmail(
                    booking,
                    booking.BookingDetails.ToList(),
                    customerName
                );
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    "[LuxeWash] Booking Successful - #" + booking.BookingId,
                    emailHtml
                );
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Background Task Error - Email] Booking #{bookingId}: {ex.Message}");
                return false;
            }
        }
        public async Task<CompatibilityDTO> CheckCompatibilityAsync(int userId, CheckCompatibilityRequestDTO request)
        {
            return await ValidateBookingCompatibilityAsync(userId, request.BranchId, request.SlotId, request.TargetDate, request.VehicleId, request.LicensePlate, request.ServiceIds);
        }
        public async Task<BookingResponseDTO> CreateBookingAsync(int userId, CreateBookingDTO request)
        {
            if (request.ServiceIds != null) request.ServiceIds = request.ServiceIds.Distinct().ToList();
            var compatibility = await ValidateBookingCompatibilityAsync(userId, request.BranchId, request.SlotId, request.ScheduledDate, request.VehicleId, request.LicensePlate, request.ServiceIds);
            if (!compatibility.IsCompatible)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException(compatibility.Message ?? "Insufficient shop capacity for your request.");
            }
            var slot = await _context.TimeSlots.FindAsync(request.SlotId);
            var targetDateTime = request.ScheduledDate.Date.Add(slot.StartTime);
            var pendingDetails = new List<BookingDetail>();
            var vehicleTypeQuery = await _context.Vehicles
                .Where(v => v.LicensePlate == request.LicensePlate && v.UserId == userId && !v.IsDeleted)
                .Select(v => new { VehicleId = v.Id, v.LicensePlate, v.VehicleType.BaseWeight, v.VehicleTypeId })
                .FirstOrDefaultAsync();
            if (vehicleTypeQuery == null)
            {
                 throw new AutoWashPro.BLL.Exceptions.NotFoundException("Vehicle not found.");
            }
            var servicePrices = await _context.ServicePrices
                .Include(sp => sp.Service)
                .Where(sp => request.ServiceIds.Contains(sp.ServiceId) && sp.VehicleTypeId == vehicleTypeQuery.VehicleTypeId && sp.BranchId == request.BranchId)
                .ToListAsync();
            decimal totalOriginalPrice = 0;
            int maxCapacityWeight = 0;
            foreach (var serviceId in request.ServiceIds)
            {
                var sp = servicePrices.FirstOrDefault(s => s.ServiceId == serviceId);
                if (sp == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Service {serviceId} is not supported for this vehicle type.");
                var actualWeight = sp.CapacityWeight > 0 ? sp.CapacityWeight : vehicleTypeQuery.BaseWeight;
                if (actualWeight > maxCapacityWeight) maxCapacityWeight = actualWeight;
                totalOriginalPrice += sp.Price;
                pendingDetails.Add(new BookingDetail
                {
                    ServiceId = serviceId,
                    Price = sp.Price
                });
            }
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var dailyCapacity = await _context.DailySlotCapacities.FirstOrDefaultAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == request.BranchId && dc.Date == targetDateTime.Date);
            if (dailyCapacity == null)
            {
                dailyCapacity = new DailySlotCapacity
                {
                    SlotId = slot.SlotId,
                    BranchId = request.BranchId,
                    Date = targetDateTime.Date,
                    BookedWeight = 0
                };
                _context.DailySlotCapacities.Add(dailyCapacity);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _context.Entry(dailyCapacity).State = EntityState.Detached;
                    dailyCapacity = await _context.DailySlotCapacities.FirstAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == request.BranchId && dc.Date == targetDateTime.Date);
                }
            }
            if (dailyCapacity.BookedWeight + maxCapacityWeight > slot.MaxCapacity)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Insufficient shop capacity for this vehicle. Please choose another time slot.");
            var (voucherDiscount, pointDiscount, pointsUsed, finalAmount, userVoucher) =
                await CalculateBookingPricingAsync(userId, totalOriginalPrice, request.VoucherId, request.PointsToUse, targetDateTime, vehicleTypeQuery.VehicleTypeId, request.BranchId);
            var paymentMethod = request.PaymentMethod?.Trim() ?? "Wallet";
            var isPayOsPayment = string.Equals(paymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "QR", StringComparison.OrdinalIgnoreCase);
            var isWalletPayment = string.Equals(paymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase);
            if (!isPayOsPayment && !isWalletPayment)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid payment method. Only Wallet or QR are supported.");
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0, Status = "Active" };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }
            if (!isPayOsPayment && wallet.Balance < finalAmount)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Insufficient wallet balance for deposit. Needed: {finalAmount:N0} VND");
            try
            {
                dailyCapacity.BookedWeight += maxCapacityWeight;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Someone else just booked a slot. Please try again.");
                }
                Transaction? paymentTx = null;
                if (!isPayOsPayment)
                {
                    wallet.Balance -= finalAmount;
                    paymentTx = new Transaction
                {
                    WalletId = wallet.WalletId,
                    Amount = -finalAmount,
                    TransactionType = "Payment",
                    Description = $"Deposit payment for car wash booking at {targetDateTime:dd/MM/yyyy HH:mm}"
                    };
                    _context.Transactions.Add(paymentTx);
                }
                if (userVoucher != null)
                {
                    userVoucher.UsageCount += 1;
                    userVoucher.IsUsed = userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser;
                    userVoucher.UsedDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                    userVoucher.LastUsedDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                    userVoucher.Voucher.CurrentUsageCount += 1;
                }
                if (pointsUsed > 0)
                {
                    await _walletService.DeductSpendablePointsAsync(userId, pointsUsed, "Use points for booking discount");
                }
                var booking = new Booking
                {
                    UserId = userId,
                    VehicleId = vehicleTypeQuery.VehicleId,
                    ActualVehicleTypeId = vehicleTypeQuery.VehicleTypeId,
                    LicensePlate = request.LicensePlate,
                    CapacityWeight = maxCapacityWeight,
                    VehicleCondition = VehicleCondition.Clean,
                    BranchId = request.BranchId,
                    ScheduledTime = targetDateTime,
                    Status = "Pending",
                    OriginalPrice = totalOriginalPrice,
                    PointsUsed = pointsUsed,
                    PointDiscountAmount = pointDiscount,
                    AppliedVoucherId = request.VoucherId,
                    VoucherDiscountAmount = voucherDiscount,
                    FinalAmount = finalAmount,
                    BookingDetails = pendingDetails,
                    FallbackQrCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper() 
                };
                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                if (paymentTx != null)
                {
                    paymentTx.ReferenceBookingId = booking.BookingId;
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                if (!isPayOsPayment)
                {
                    var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        var emailHtml = EmailTemplateBuilder.BuildBookingConfirmationEmail(booking, pendingDetails, user.CustomerProfile?.FullName ?? "Valued Customer");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendEmailAsync(user.Email, "[LuxeWash] Booking Successful - #" + booking.BookingId, emailHtml);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Background Email Error]: {ex.Message}");
                            }
                        });
                    }
                }
                var serviceNames = await _context.Services.Where(s => request.ServiceIds.Contains(s.ServiceId)).Select(s => s.ServiceName).ToListAsync();
                
                await _userNotificationService.CreateNotificationAsync(
                    userId,
                    "Ä áº·t lá»‹ch thÃ nh cÃ´ng",
                    $"Lá»‹ch Ä‘áº·t rá»­a xe cho biá»ƒn sá»‘ {request.LicensePlate} vÃ o lÃºc {targetDateTime:dd/MM/yyyy HH:mm} Ä‘Ã£ Ä‘Æ°á»£c ghi nháº­n.",
                    "Booking",
                    booking.BookingId.ToString()
                );

                return new BookingResponseDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate,
                    ServiceNames = serviceNames,
                    ScheduledTime = booking.ScheduledTime,
                    Status = booking.Status,
                    OriginalPrice = booking.OriginalPrice,
                    PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                    AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    FinalAmount = booking.FinalAmount,
                    ProcessingStartTime = booking.ProcessingStartTime.HasValue ? booking.ProcessingStartTime.Value : (DateTime?)null,
                    CompletedTime = booking.CompletedTime.HasValue ? booking.CompletedTime.Value : (DateTime?)null,
                    ActualDurationMinutes = booking.ActualDurationMinutes
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<BookingPaymentLinkResponseDTO> CreateBookingPaymentLinkAsync(int userId, int bookingId, CreateBookingPaymentLinkDTO request)
        {
            var result = await _walletService.CreatePaymentQrAsync(userId, new PaymentQrRequestDTO
            {
                PaymentType = "BookingPayment",
                BookingId = bookingId,
                CancelUrl = request.CancelUrl,
                ReturnUrl = request.ReturnUrl
            });
            return new BookingPaymentLinkResponseDTO
            {
                BookingId = result.BookingId ?? bookingId,
                Amount = result.Amount,
                OrderCode = result.OrderCode,
                PaymentUrl = result.PaymentUrl
            };
        }
        public async Task<List<BookingResponseDTO>> GetMyBookingsAsync(int userId, int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;
            var nowUtc = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var bookings = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.Branch)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.ScheduledTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BookingResponseDTO
                {
                    BookingId = b.BookingId,
                    BranchId = b.BranchId,
                    BranchName = b.Branch != null ? b.Branch.Name : "",
                    IsWaitAccepted = b.IsWaitAccepted,
                    HasPendingOverloadSuggestion = false, 
                    LicensePlate = b.LicensePlate ?? "",
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointsUsed = b.PointsUsed,
                    PointDiscountAmount = b.PointDiscountAmount,
                    AppliedVoucherId = b.AppliedVoucherId,
                    AppliedVoucherCode = b.AppliedVoucher != null ? b.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount,
                    ProcessingStartTime = b.ProcessingStartTime,
                    CompletedTime = b.CompletedTime,
                    ActualDurationMinutes = b.ActualDurationMinutes,
                    CheckInImageUrl = b.CheckInImageUrl,
                    CheckOutImageUrl = b.CheckOutImageUrl
                })
                .ToListAsync();
            var proposals = await GetRelocationProposalsAsync(userId);
            var activeSuggestions = await _context.OverloadSuggestions
                .Where(s => s.Booking.UserId == userId && !s.IsProcessed && s.ExpiresAt > nowUtc)
                .Select(s => s.BookingId)
                .Distinct()
                .ToListAsync();
            var bookingIds = bookings.Select(b => b.BookingId).ToList();
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
                    t.TransactionType
                })
                .ToListAsync();
            var latestTxByBooking = paymentTransactions
                .GroupBy(t => t.BookingId)
                .ToDictionary(g => g.Key, g => new
                {
                    Tx = g.FirstOrDefault(x => x.TransactionType != "Refund"),
                    IsRefunded = g.Any(x => x.TransactionType == "Refund")
                });
            foreach (var b in bookings)
            {
                if (latestTxByBooking.TryGetValue(b.BookingId, out var paymentData))
                {
                    if (paymentData.IsRefunded)
                    {
                        b.PaymentStatus = "Refunded";
                        b.PaymentMethod = paymentData.Tx?.PaymentMethod;
                    }
                    else if (paymentData.Tx != null)
                    {
                        b.PaymentStatus = paymentData.Tx.Status?.ToLowerInvariant() switch
                        {
                            "completed" => "Completed",
                            "pending"   => "Pending",
                            "expired"   => "Expired",
                            "failed"    => "Failed",
                            "cancelled" => "Failed",
                            _           => "Unpaid"
                        };
                        b.PaymentMethod = paymentData.Tx.PaymentMethod;
                    }
                    else
                    {
                        b.PaymentStatus = "Unpaid";
                        b.PaymentMethod = null;
                    }
                }
                else
                {
                    b.PaymentStatus = "Unpaid";
                    b.PaymentMethod = null;
                }
                if (b.ProcessingStartTime.HasValue) b.ProcessingStartTime = b.ProcessingStartTime.Value;
                if (b.CompletedTime.HasValue) b.CompletedTime = b.CompletedTime.Value;
                var proposal = proposals.FirstOrDefault(p => p.BookingId == b.BookingId);
                if (proposal != null)
                {
                    b.HasPendingRelocation = true;
                    b.Relocation = proposal;
                }
                if (activeSuggestions.Contains(b.BookingId))
                {
                    b.HasPendingOverloadSuggestion = true;
                }
            }
            return bookings;
        }
        public async Task<List<RelocationProposalCustomerDTO>> GetRelocationProposalsAsync(int userId)
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var pendingBookings = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.Branch)
                .Include(b => b.BookingDetails).ThenInclude(d => d.Service)
                .Where(b => b.UserId == userId && b.Status == "Pending" && b.ScheduledTime >= now)
                .ToListAsync();
            if (pendingBookings.Count == 0) return new List<RelocationProposalCustomerDTO>();

            var voucherCodes = pendingBookings
                .Select(b => $"SURGE_REL_{b.BranchId}_{b.BookingId}")
                .ToList();

            var relocationVouchers = await _context.Vouchers
                .Include(v => v.Branch)
                .Where(v => voucherCodes.Contains(v.Code)
                    && v.IsActive
                    && v.CurrentUsageCount == 0
                    && v.ExpiryDate > now)
                .AsNoTracking()
                .ToDictionaryAsync(v => v.Code);

            var proposals = new List<RelocationProposalCustomerDTO>();
            foreach (var booking in pendingBookings)
            {
                string voucherCodePrefix = $"SURGE_REL_{booking.BranchId}_{booking.BookingId}";
                if (!relocationVouchers.TryGetValue(voucherCodePrefix, out var relocationVoucher))
                    continue;
                if (relocationVoucher.Branch == null)
                    continue;

                proposals.Add(new RelocationProposalCustomerDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate ?? "",
                    ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = booking.ScheduledTime,
                    OriginalBranchId = booking.BranchId,
                    OriginalBranchName = booking.Branch?.Name ?? "",
                    AlternativeBranchId = relocationVoucher.BranchId!.Value,
                    AlternativeBranchName = relocationVoucher.Branch.Name,
                    AlternativeBranchAddress = relocationVoucher.Branch.Address ?? "",
                    AlternativeBranchDistanceKm = 2.8, 
                    VoucherCode = relocationVoucher.Code,
                    VoucherDiscountAmount = relocationVoucher.DiscountAmount,
                    ProposalExpiresAt = relocationVoucher.ExpiryDate
                });
            }
            return proposals;
        }
        public async Task<bool> CancelBookingAsync(int userId, int bookingId)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found.");
            if (booking.Status != "Pending") throw new AutoWashPro.BLL.Exceptions.BadRequestException("Can only cancel bookings in Pending status.");
            
            var vietnamTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddHours(7);
            var totalHours = (booking.ScheduledTime - vietnamTime).TotalHours;
            if (totalHours > -1 && totalHours < 2)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Quý khách chỉ có thể hủy lịch đặt trước ít nhất 2 tiếng so với giờ hẹn.");
            }

            bool isRefundable = (booking.ScheduledTime - vietnamTime).TotalHours >= 4;
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                booking.Status = "Cancelled";
                var slot = await _context.TimeSlots.FirstOrDefaultAsync(s => s.BranchId == booking.BranchId && s.StartTime == booking.ScheduledTime.TimeOfDay);
                if (slot != null)
                {
                    var dailyCapacity = await _context.DailySlotCapacities
                        .FirstOrDefaultAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == booking.BranchId && dc.Date == booking.ScheduledTime.Date);
                    if (dailyCapacity != null && dailyCapacity.BookedWeight > 0)
                    {
                        dailyCapacity.BookedWeight -= booking.CapacityWeight;
                        if(dailyCapacity.BookedWeight < 0) dailyCapacity.BookedWeight = 0;
                    }
                }
                if (isRefundable)
                {
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet != null && booking.FinalAmount > 0 && await HasCompletedBookingPaymentAsync(booking.BookingId))
                    {
                        wallet.Balance += booking.FinalAmount;
                        var refundTx = new Transaction
                        {
                            WalletId = wallet.WalletId,
                            Amount = booking.FinalAmount,
                            TransactionType = "Refund",
                            Description = $"Refund deposit for cancelled booking #{booking.BookingId}",
                            ReferenceBookingId = booking.BookingId
                        };
                        _context.Transactions.Add(refundTx);
                    }
                    if (booking.PointsUsed > 0)
                    {
                        await _walletService.RefundSpendablePointsAsync(
                            userId,
                            booking.PointsUsed,
                            $"{PointConstants.RefundPointsReasonPrefix} #{booking.BookingId}",
                            booking.BookingId);
                    }
                    if (booking.AppliedVoucherId.HasValue)
                    {
                        var userVoucher = await _context.UserVouchers
                            .Include(uv => uv.Voucher)
                            .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == booking.AppliedVoucherId.Value);
                        if (userVoucher != null)
                        {
                            if (userVoucher.UsageCount > 0)
                            {
                                userVoucher.UsageCount -= 1;
                            }
                            userVoucher.IsUsed = false;
                            userVoucher.UsedDate = userVoucher.UsageCount > 0 ? userVoucher.UsedDate : null;
                            userVoucher.LastUsedDate = userVoucher.UsageCount > 0 ? userVoucher.LastUsedDate : null;
                            if (userVoucher.Voucher.CurrentUsageCount > 0)
                            {
                                userVoucher.Voucher.CurrentUsageCount -= 1;
                            }
                        }
                    }
                }
                else
                {
                    var userProfile = await _context.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
                }
                booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("An error occurred while canceling the booking (data conflict). Please try again.");
                }
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private async Task<(decimal voucherDiscount, decimal pointDiscount, int pointsUsed, decimal finalAmount, UserVoucher? userVoucher)>
            CalculateBookingPricingAsync(int userId, decimal originalPrice, int? voucherId, int pointsToUseRequest, DateTime scheduledTime, int vehicleTypeId, int branchId)
        {
            var customerProfile = await _context.CustomerProfiles
                .Include(cp => cp.Tier)
                .FirstOrDefaultAsync(cp => cp.UserId == userId);
            if (customerProfile == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Customer profile not found.");

            // Membership pricing is a server-side rule. The client may preview
            // the discount, but wallet deductions and PayOS must use this value.
            var membershipDiscountRate = GetMembershipDiscountRate(customerProfile.Tier?.TierName);
            var membershipDiscount = decimal.Round(
                originalPrice * membershipDiscountRate,
                0,
                MidpointRounding.AwayFromZero);
            var priceAfterMembership = Math.Max(0, originalPrice - membershipDiscount);

            decimal voucherDiscount = 0;
            UserVoucher? userVoucher = null;
            if (voucherId.HasValue)
            {
                userVoucher = await _context.UserVouchers
                    .Include(uv => uv.Voucher)
                        .ThenInclude(v => v.Branch)
                    .FirstOrDefaultAsync(uv => uv.VoucherId == voucherId.Value && uv.UserId == userId);
                if (userVoucher == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("You do not own this voucher.");
                if (userVoucher.Voucher.BranchId.HasValue && userVoucher.Voucher.BranchId.Value != branchId)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"This voucher is only valid for use at branch #{userVoucher.Voucher.BranchId.Value} ({userVoucher.Voucher.Branch?.Name ?? "Designated Branch"}).");
                }
                if (userVoucher.Voucher.VehicleTypeId.HasValue && userVoucher.Voucher.VehicleTypeId.Value != vehicleTypeId)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("This voucher does not apply to your vehicle type.");
                }
                if (!userVoucher.Voucher.IsActive) throw new AutoWashPro.BLL.Exceptions.BadRequestException("This voucher is not activated.");
                if (userVoucher.Voucher.StartDate.HasValue && userVoucher.Voucher.StartDate.Value > AutoWashPro.DAL.Helpers.TimeHelper.VnNow) throw new AutoWashPro.BLL.Exceptions.BadRequestException("This voucher is not yet valid.");
                if (userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser) throw new AutoWashPro.BLL.Exceptions.BadRequestException("You have reached your usage limit for this voucher.");
                if (userVoucher.Voucher.MaxUsages > 0 && userVoucher.Voucher.CurrentUsageCount >= userVoucher.Voucher.MaxUsages) throw new AutoWashPro.BLL.Exceptions.BadRequestException("This voucher has reached its total usage limit.");
                if (userVoucher.ExpiryDate < AutoWashPro.DAL.Helpers.TimeHelper.VnNow) throw new AutoWashPro.BLL.Exceptions.BadRequestException("This voucher has expired.");
                if (userVoucher.Voucher.MinOrderAmount > 0 && priceAfterMembership < userVoucher.Voucher.MinOrderAmount)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"This voucher only applies to orders from {userVoucher.Voucher.MinOrderAmount:N0} VND.");
                if (userVoucher.Voucher.VoucherType == AutoWashPro.DAL.Enums.VoucherType.PhysicalGift)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Physical gift voucher cannot be used to discount invoices.");
                }
                if (userVoucher.Voucher.ValidStartTime.HasValue && userVoucher.Voucher.ValidEndTime.HasValue)
                {
                    var timeOfDay = scheduledTime.TimeOfDay;
                    var startTime = userVoucher.Voucher.ValidStartTime.Value;
                    var endTime = userVoucher.Voucher.ValidEndTime.Value;
                    bool isValidTime = false;
                    if (startTime <= endTime)
                    {
                        isValidTime = timeOfDay >= startTime && timeOfDay <= endTime;
                    }
                    else
                    {
                        isValidTime = timeOfDay >= startTime || timeOfDay <= endTime;
                    }
                    if (!isValidTime)
                    {
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException($"This Happy Hour voucher is only valid during the time slot from {startTime:hh\\:mm} to {endTime:hh\\:mm}.");
                    }
                }
                voucherDiscount = Math.Min(userVoucher.Voucher.DiscountAmount, priceAfterMembership);
            }
            decimal remainingAfterVoucher = priceAfterMembership - voucherDiscount;
            int pointsUsed = 0;
            decimal pointDiscount = 0;
            if (pointsToUseRequest > 0)
            {
                int maxPointsByBalance = customerProfile.TotalPoint;
                int maxPointsByMoney = (int)(remainingAfterVoucher / PointConstants.VndPerSpendPoint);
                int pointsToApply = Math.Min(pointsToUseRequest, Math.Min(maxPointsByBalance, maxPointsByMoney));
                if (pointsToApply < pointsToUseRequest && pointsToApply == 0)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Not enough points or remaining balance does not allow point usage.");
                pointsUsed = pointsToApply;
                pointDiscount = pointsUsed * PointConstants.VndPerSpendPoint;
            }
            decimal finalAmount = remainingAfterVoucher - pointDiscount;
            if (finalAmount < 0) finalAmount = 0;
            return (voucherDiscount, pointDiscount, pointsUsed, finalAmount, userVoucher);
        }

        private static decimal GetMembershipDiscountRate(string? tierName)
        {
            return tierName?.Trim().ToLowerInvariant() switch
            {
                "silver" => 0.05m,
                "gold" => 0.10m,
                "platinum" => 0.15m,
                "diamond" => 0.20m,
                _ => 0m
            };
        }
        public async Task<bool> UpdateVehicleConditionAsync(int staffId, int bookingId, UpdateVehicleConditionDTO request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                    .Include(b => b.BookingDetails)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId);
                if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found.");
                if (booking.Status != "CheckedIn")
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Can only update vehicle condition when the vehicle is checked in at the station.");
                booking.VehicleCondition = request.Condition;
                decimal newSurcharge = 0;
                var basePrice = booking.BookingDetails.Sum(bd => bd.Price);
                if (request.Condition == VehicleCondition.Dirty)
                {
                    newSurcharge += basePrice * 0.2m; 
                }
                else if (request.Condition == VehicleCondition.VeryDirty)
                {
                    newSurcharge += basePrice * 0.5m; 
                }
                if (request.ActualVehicleTypeId.HasValue)
                {
                    booking.ActualVehicleTypeId = request.ActualVehicleTypeId.Value;
                    newSurcharge += basePrice * 0.3m;
                }
                decimal surchargeDiff = newSurcharge - booking.MismatchSurcharge;
                booking.MismatchSurcharge = newSurcharge;
                booking.OriginalPrice += surchargeDiff;
                booking.FinalAmount += surchargeDiff;
                if (surchargeDiff != 0 && booking.UserId.HasValue)
                {
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == booking.UserId.Value);
                    if (wallet != null)
                    {
                        if (surchargeDiff > 0)
                        {
                            if (wallet.Balance < surchargeDiff)
                            {
                                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Customer balance is insufficient to pay surcharge. Need additional: {surchargeDiff:N0} VND");
                            }
                            wallet.Balance -= surchargeDiff;
                            var paymentTx = new Transaction
                            {
                                WalletId = wallet.WalletId,
                                Amount = -surchargeDiff,
                                TransactionType = "Payment",
                                Description = $"Payment for dirty vehicle surcharge for booking #{booking.BookingId}",
                                ReferenceBookingId = booking.BookingId
                            };
                            _context.Transactions.Add(paymentTx);
                        }
                        else if (surchargeDiff < 0)
                        {
                            decimal refundAmount = Math.Abs(surchargeDiff);
                            wallet.Balance += refundAmount;
                            var refundTx = new Transaction
                            {
                                WalletId = wallet.WalletId,
                                Amount = refundAmount,
                                TransactionType = "Refund",
                                Description = $"Refund vehicle condition surcharge for booking #{booking.BookingId}",
                                ReferenceBookingId = booking.BookingId
                            };
                            _context.Transactions.Add(refundTx);
                        }
                    }
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task MarkAsNoShowAsync(int bookingId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found.");
            booking.Status = "NoShow";
            await _context.SaveChangesAsync();
        }
        public async Task ReportMismatchAsync(int bookingId, AutoWashPro.BLL.Enums.VehicleConditionEnum condition, int actualTypeId)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found.");

            var entityCondition = (AutoWashPro.DAL.Entities.VehicleCondition)condition;
            booking.VehicleCondition = entityCondition;
            booking.ActualVehicleTypeId = actualTypeId;

            var serviceIds = booking.BookingDetails.Select(d => d.ServiceId).ToHashSet();
            var prices = await _context.ServicePrices
                .Where(sp => serviceIds.Contains(sp.ServiceId)
                          && sp.VehicleTypeId == actualTypeId
                          && sp.BranchId == booking.BranchId)
                .Select(sp => new { sp.ServiceId, sp.Price })
                .AsNoTracking()
                .ToDictionaryAsync(x => x.ServiceId, x => x.Price);

            var multiplier = entityCondition == AutoWashPro.DAL.Entities.VehicleCondition.VeryDirty ? 1.2m : 1.0m;
            var totalOldPrice = booking.BookingDetails.Sum(d => d.Price);
            var totalNewPrice = booking.BookingDetails.Sum(d =>
                prices.TryGetValue(d.ServiceId, out var p) ? p * multiplier : 0m);

            if (totalNewPrice > totalOldPrice)
            {
                booking.MismatchSurcharge = totalNewPrice - totalOldPrice;
                _logger.LogInformation(
                    "Vehicle mismatch surcharge of {Surcharge} VND applied to booking {BookingId}.",
                    booking.MismatchSurcharge, booking.BookingId);
            }
            await _context.SaveChangesAsync();
        }
        public async Task<WalkInBookingResponseDTO> CreateWalkInBookingAsync(int staffId, CreateWalkInBookingDTO request)
        {
            if (request.ServiceIds != null) request.ServiceIds = request.ServiceIds.Distinct().ToList();
            if (request.ServiceIds == null || request.ServiceIds.Count == 0)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Please select at least 1 service.");
            int? customerUserId = request.UserId == 0 ? (int?)null : request.UserId;
            var targetDateTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var normalizedPlate = request.LicensePlate.Replace("-", "").Replace(".", "").Trim().ToUpper();
            bool hasActiveBooking = await _context.Bookings.Include(b => b.AppliedVoucher).AnyAsync(b => b.LicensePlate == normalizedPlate && (b.Status == "Pending" || b.Status == "CheckedIn"));
            if (hasActiveBooking)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Vehicle with license plate {normalizedPlate} has an unfinished booking.");
            var existingVehicle = await _context.Vehicles
                .Include(v => v.VehicleType)
                .Where(v => v.LicensePlate == normalizedPlate)
                .FirstOrDefaultAsync();
            int resolvedVehicleTypeId;
            int resolvedVehicleId;
            int resolvedBaseWeight;
            if (existingVehicle != null)
            {
                if (existingVehicle.IsDeleted)
                {
                    existingVehicle.IsDeleted = false;
                }
                if (request.VehicleTypeId.HasValue && request.VehicleTypeId.Value > 0
                    && request.VehicleTypeId.Value != existingVehicle.VehicleTypeId)
                {
                    existingVehicle.VehicleTypeId = request.VehicleTypeId.Value;
                    var updatedType = await _context.VehicleTypes
                        .FirstOrDefaultAsync(vt => vt.Id == request.VehicleTypeId.Value);
                    if (updatedType != null)
                    {
                        resolvedBaseWeight = updatedType.BaseWeight;
                        resolvedVehicleTypeId = updatedType.Id;
                    }
                    else
                    {
                        resolvedBaseWeight = existingVehicle.VehicleType?.BaseWeight ?? 1;
                        resolvedVehicleTypeId = existingVehicle.VehicleTypeId;
                    }
                }
                else
                {
                    resolvedBaseWeight = existingVehicle.VehicleType?.BaseWeight ?? 1;
                    resolvedVehicleTypeId = existingVehicle.VehicleTypeId;
                }
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new Exception($"Database update failed during Vehicle restore: {ex.InnerException?.Message ?? ex.Message}", ex);
                }
                resolvedVehicleId = existingVehicle.Id;
            }
            else if (request.VehicleTypeId.HasValue && request.VehicleTypeId.Value > 0)
            {
                var requestedType = await _context.VehicleTypes
                    .FirstOrDefaultAsync(vt => vt.Id == request.VehicleTypeId.Value);
                if (requestedType == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Vehicle type {request.VehicleTypeId.Value} does not exist.");
                var otherCarModel = await _context.CarModels.FirstOrDefaultAsync(cm => cm.Name == "Other");
                if (otherCarModel == null)
                {
                    otherCarModel = new CarModel { Name = "Other", Brand = "Other" };
                    _context.CarModels.Add(otherCarModel);
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        throw new Exception($"Database update failed during CarModel creation: {ex.InnerException?.Message ?? ex.Message}", ex);
                    }
                }
                var newVehicle = new Vehicle
                {
                    UserId = customerUserId,
                    LicensePlate = normalizedPlate,
                    VehicleTypeId = requestedType.Id,
                    CarModelId = otherCarModel.Id
                };
                _context.Vehicles.Add(newVehicle);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new Exception($"Database update failed during Vehicle creation: {ex.InnerException?.Message ?? ex.Message}", ex);
                }
                resolvedVehicleId = newVehicle.Id;
                resolvedBaseWeight = requestedType.BaseWeight;
                resolvedVehicleTypeId = requestedType.Id;
            }
            else
            {
                var otherVehicleType = await _context.VehicleTypes.FirstOrDefaultAsync(vt => vt.Name == "Other");
                if (otherVehicleType == null)
                {
                    otherVehicleType = new VehicleType { Name = "Other", BaseWeight = 1 };
                    _context.VehicleTypes.Add(otherVehicleType);
                    await _context.SaveChangesAsync();
                }
                var otherCarModel = await _context.CarModels.FirstOrDefaultAsync(cm => cm.Name == "Other");
                if (otherCarModel == null)
                {
                    otherCarModel = new CarModel { Name = "Other", Brand = "Other" };
                    _context.CarModels.Add(otherCarModel);
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        throw new Exception($"Database update failed during CarModel creation: {ex.InnerException?.Message ?? ex.Message}", ex);
                    }
                }
                var newVehicle = new Vehicle
                {
                    UserId = customerUserId,
                    LicensePlate = normalizedPlate,
                    VehicleTypeId = otherVehicleType.Id,
                    CarModelId = otherCarModel.Id
                };
                _context.Vehicles.Add(newVehicle);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    throw new Exception($"Database update failed during Vehicle creation: {ex.InnerException?.Message ?? ex.Message}", ex);
                }
                resolvedVehicleId = newVehicle.Id;
                resolvedBaseWeight = otherVehicleType.BaseWeight;
                resolvedVehicleTypeId = otherVehicleType.Id;
            }
            var vehicleTypeQuery = new { VehicleId = resolvedVehicleId, LicensePlate = normalizedPlate, BaseWeight = resolvedBaseWeight, VehicleTypeId = resolvedVehicleTypeId };
            var servicePrices = await _context.ServicePrices
                .Include(sp => sp.Service)
                .Where(sp => request.ServiceIds.Contains(sp.ServiceId) && sp.VehicleTypeId == vehicleTypeQuery.VehicleTypeId && sp.BranchId == request.BranchId)
                .ToListAsync();
            decimal totalOriginalPrice = 0;
            int maxCapacityWeight = 0;
            var pendingDetails = new List<BookingDetail>();
            foreach (var serviceId in request.ServiceIds)
            {
                var sp = servicePrices.FirstOrDefault(s => s.ServiceId == serviceId);
                if (sp == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Service {serviceId} does not support this vehicle type at this branch.");
                var actualWeight = sp.CapacityWeight > 0 ? sp.CapacityWeight : vehicleTypeQuery.BaseWeight;
                if (actualWeight > maxCapacityWeight) maxCapacityWeight = actualWeight;
                totalOriginalPrice += sp.Price;
                pendingDetails.Add(new BookingDetail
                {
                    ServiceId = serviceId,
                    Price = sp.Price
                });
            }
            var timeOfDay = targetDateTime.TimeOfDay;
            var slot = await _context.TimeSlots
                .Where(s => s.BranchId == request.BranchId && s.StartTime <= timeOfDay && s.EndTime >= timeOfDay)
                .FirstOrDefaultAsync();
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (slot != null)
            {
                var dailyCapacity = await _context.DailySlotCapacities.FirstOrDefaultAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == slot.BranchId && dc.Date == targetDateTime.Date);
                if (dailyCapacity == null)
                {
                    dailyCapacity = new DailySlotCapacity
                    {
                        SlotId = slot.SlotId,
                        BranchId = slot.BranchId,
                        Date = targetDateTime.Date,
                        BookedWeight = 0
                    };
                    _context.DailySlotCapacities.Add(dailyCapacity);
                    try { await _context.SaveChangesAsync(); }
                    catch (DbUpdateException)
                    {
                        _context.Entry(dailyCapacity).State = EntityState.Detached;
                        dailyCapacity = await _context.DailySlotCapacities.FirstAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == slot.BranchId && dc.Date == targetDateTime.Date);
                    }
                }
                if (!request.ForceOverrideCapacity && dailyCapacity.BookedWeight + maxCapacityWeight > slot.MaxCapacity)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Insufficient shop capacity for this vehicle right now. Please try again later.");
                dailyCapacity.BookedWeight += maxCapacityWeight;
            }
            string? paymentUrl = null;
            try
            {
                if (slot != null)
                {
                    try { await _context.SaveChangesAsync(); }
                    catch (DbUpdateConcurrencyException) { throw new AutoWashPro.BLL.Exceptions.BadRequestException("Someone else just booked a slot. Please try again."); }
                }
                var paymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "Cash" : request.PaymentMethod.Trim();
                var isWalletPayment = string.Equals(paymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase);
                var isCashPayment = string.Equals(paymentMethod, "Cash", StringComparison.OrdinalIgnoreCase);
                var isPayOsPayment = string.Equals(paymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase);
                if (!isWalletPayment && !isCashPayment && !isPayOsPayment)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid payment method. Only Cash, PayOS, or Wallet are supported.");
                }
                paymentMethod = isWalletPayment ? "Wallet" : isPayOsPayment ? "PayOS" : "Cash";
                Booking booking;
                Transaction paymentTx;
                if (customerUserId == null) 
                {
                    if (isWalletPayment)
                    {
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException("Walk-in customers cannot pay using Wallet.");
                    }
                    decimal finalAmount = totalOriginalPrice;
                    booking = new Booking
                    {
                        UserId = null,
                        VehicleId = vehicleTypeQuery.VehicleId,
                        ActualVehicleTypeId = vehicleTypeQuery.VehicleTypeId,
                        LicensePlate = normalizedPlate,
                        CapacityWeight = maxCapacityWeight,
                        VehicleCondition = VehicleCondition.Clean,
                        BranchId = request.BranchId,
                        ScheduledTime = targetDateTime,
                        Status = "CheckedIn",
                        OriginalPrice = totalOriginalPrice,
                        PointsUsed = 0,
                        PointDiscountAmount = 0,
                        AppliedVoucherId = null,
                        VoucherDiscountAmount = 0,
                        FinalAmount = finalAmount,
                        BookingDetails = pendingDetails,
                        FallbackQrCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                    };
                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();
                    var isPayOsWalkInPayment = string.Equals(paymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase);
                    long? payOsOrderCode = null;
                    int? payOsAmount = null;
                    if (isPayOsWalkInPayment)
                    {
                        if (finalAmount <= 0)
                            throw new AutoWashPro.BLL.Exceptions.BadRequestException("Khong the tao link thanh toan PayOS vi tong tien dich vu khong hop le.");
                        payOsAmount = ToPayOsAmount(finalAmount);
                        payOsOrderCode = GeneratePayOsOrderCode();
                    }
                    paymentTx = new Transaction
                    {
                        WalletId = null,
                        Amount = payOsAmount ?? finalAmount,
                        TransactionType = "WalkInPayment",
                        Description = $"Walk-in payment via {paymentMethod}",
                        PaymentMethod = paymentMethod,
                        ReferenceBookingId = booking.BookingId,
                        OrderCode = payOsOrderCode?.ToString(),
                        Status = isPayOsWalkInPayment ? "Pending" : "Completed",
                        CreatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow
                    };
                    _context.Transactions.Add(paymentTx);
                    await _context.SaveChangesAsync();
                    if (isPayOsWalkInPayment)
                    {
                        var payOsResult = await _payOsService.CreatePaymentLinkAsync(
                            payOsOrderCode!.Value,
                            payOsAmount!.Value,
                            $"Thanh toan #{booking.BookingId}",
                            "WalkIn",
                            string.IsNullOrWhiteSpace(request.ReturnUrl) ? null : request.ReturnUrl,
                            string.IsNullOrWhiteSpace(request.CancelUrl) ? null : request.CancelUrl
                        );
                        paymentUrl = payOsResult.CheckoutUrl;
                    }
                }
                else 
                {
                    var (voucherDiscount, pointDiscount, pointsUsed, finalAmount, userVoucher) =
                        await CalculateBookingPricingAsync(customerUserId.Value, totalOriginalPrice, request.VoucherId, request.PointsToUse, targetDateTime, vehicleTypeQuery.VehicleTypeId, request.BranchId);

                    if (userVoucher != null)
                    {
                        userVoucher.UsageCount += 1;
                        userVoucher.IsUsed = userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser;
                        userVoucher.UsedDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                        userVoucher.LastUsedDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                        userVoucher.Voucher.CurrentUsageCount += 1;
                    }
                    if (pointsUsed > 0)
                    {
                        await _walletService.DeductSpendablePointsAsync(customerUserId.Value, pointsUsed, "Use points for walk-in booking discount");
                    }

                    booking = new Booking
                    {
                        UserId = customerUserId,
                        VehicleId = vehicleTypeQuery.VehicleId,
                        ActualVehicleTypeId = vehicleTypeQuery.VehicleTypeId,
                        LicensePlate = normalizedPlate,
                        CapacityWeight = maxCapacityWeight,
                        VehicleCondition = VehicleCondition.Clean,
                        BranchId = request.BranchId,
                        ScheduledTime = targetDateTime,
                        Status = "CheckedIn",
                        OriginalPrice = totalOriginalPrice,
                        PointsUsed = pointsUsed,
                        PointDiscountAmount = pointDiscount,
                        AppliedVoucherId = request.VoucherId,
                        VoucherDiscountAmount = voucherDiscount,
                        FinalAmount = finalAmount,
                        BookingDetails = pendingDetails,
                        FallbackQrCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                    };
                    _context.Bookings.Add(booking);
                    await _context.SaveChangesAsync();

                    if (isWalletPayment)
                    {
                        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == customerUserId);
                        if (wallet == null || wallet.Balance < finalAmount)
                            throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Customer wallet balance is insufficient for payment. Needed: {finalAmount:N0} VND");
                        wallet.Balance -= finalAmount;
                        
                        paymentTx = new Transaction
                        {
                            WalletId = wallet.WalletId,
                            Amount = -finalAmount,
                            TransactionType = "Payment",
                            Description = $"Walk-in customer (with account) payment at {targetDateTime:dd/MM/yyyy HH:mm}",
                            PaymentMethod = paymentMethod,
                            ReferenceBookingId = booking.BookingId
                        };
                        _context.Transactions.Add(paymentTx);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        long? payOsOrderCode = null;
                        int? payOsAmount = null;
                        if (isPayOsPayment)
                        {
                            if (finalAmount <= 0)
                                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Could not create PayOS payment link because total service amount is {finalAmount:N0} VND. Please check the service price list for this vehicle type at the branch.");
                            payOsAmount = ToPayOsAmount(finalAmount);
                            payOsOrderCode = GeneratePayOsOrderCode();
                        }
                        
                        paymentTx = new Transaction
                        {
                            WalletId = null,
                            Amount = payOsAmount ?? finalAmount,
                            TransactionType = "WalkInPayment",
                            Description = $"Walk-in payment via {paymentMethod}",
                            PaymentMethod = paymentMethod,
                            ReferenceBookingId = booking.BookingId,
                            OrderCode = payOsOrderCode?.ToString(),
                            Status = payOsOrderCode != null ? "Pending" : "Completed"
                        };
                        _context.Transactions.Add(paymentTx);
                        await _context.SaveChangesAsync();

                        if (isPayOsPayment)
                        {
                            var payOsResult = await _payOsService.CreatePaymentLinkAsync(
                                payOsOrderCode!.Value,
                                payOsAmount!.Value,
                                $"Thanh toan #{booking.BookingId}",
                                "WalkIn",
                                string.IsNullOrWhiteSpace(request.ReturnUrl) ? null : request.ReturnUrl,
                                string.IsNullOrWhiteSpace(request.CancelUrl) ? null : request.CancelUrl
                            );
                            paymentUrl = payOsResult.CheckoutUrl;
                        }
                    }
                }
                int? processingLaneId = null;
                string? processingLaneName = null;
                bool isWaitingForLane = true;
                var isPaymentCompleted = await _context.Transactions.AnyAsync(t => t.ReferenceBookingId == booking.BookingId && (t.TransactionType == "WalkInPayment" || t.TransactionType == "Payment") && t.Status == "Completed");
                if (isPaymentCompleted)
                {
                    var checkInResult = await _laneCoordinator.CheckInAtEntryGateAsync(
                        booking.LicensePlate ?? "UNKNOWN",
                        request.BranchId,
                        bookingId: booking.BookingId);
                    if (!checkInResult.IsWaiting && checkInResult.LaneId.HasValue)
                    {
                        booking.ProcessingLaneId = checkInResult.LaneId.Value;
                        processingLaneId = checkInResult.LaneId.Value;
                        processingLaneName = checkInResult.LaneName;
                        isWaitingForLane = false;
                        await _context.SaveChangesAsync();
                    }
                }
                await transaction.CommitAsync();
                var serviceNames = await _context.Services.Where(s => request.ServiceIds.Contains(s.ServiceId)).Select(s => s.ServiceName).ToListAsync();
                return new WalkInBookingResponseDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate,
                    ServiceNames = serviceNames,
                    ScheduledTime = booking.ScheduledTime,
                    Status = booking.Status,
                    OriginalPrice = booking.OriginalPrice,
                    PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                    AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    FinalAmount = booking.FinalAmount,
                    PaymentUrl = paymentUrl,
                    ProcessingLaneId = processingLaneId,
                    ProcessingLaneName = processingLaneName,
                    IsWaitingForLane = isWaitingForLane
                };
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Database update failed: {ex.InnerException?.Message ?? ex.Message}", ex);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task ForceCancelBookingsAsync(ForceCancelRequestDTO request)
        {
            if (!request.TimeSlotId.HasValue && !request.AffectedDate.HasValue)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Please select a date or time slot to cancel the booking.");

            TimeSlot? slot = null;
            if (request.TimeSlotId.HasValue)
            {
                slot = await _context.TimeSlots.FindAsync(request.TimeSlotId.Value);
            }

            var query = _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.User)
                .Where(b => b.Status == BookingStatuses.Pending && b.BranchId == request.BranchId);
            if (request.AffectedDate.HasValue)
            {
                var targetDate = request.AffectedDate.Value.Date;
                query = query.Where(b => b.ScheduledTime.Date == targetDate);
            }
            if (slot != null)
            {
                query = query.Where(b => b.ScheduledTime.TimeOfDay >= slot.StartTime && b.ScheduledTime.TimeOfDay <= slot.EndTime);
            }
            var bookings = await query.ToListAsync();
            if (!bookings.Any()) return;

            var bookingIds = bookings.Select(b => b.BookingId).ToList();
            var pendingTxs = await _context.Transactions
                .Where(t => t.ReferenceBookingId.HasValue
                         && bookingIds.Contains(t.ReferenceBookingId.Value)
                         && t.Status == "Pending"
                         && (t.TransactionType == "BookingPayment" || t.TransactionType == "WalkInPayment"))
                .ToListAsync();
            var pendingTxsByBooking = pendingTxs
                .Where(t => t.ReferenceBookingId.HasValue)
                .ToLookup(t => t.ReferenceBookingId!.Value, t => t);

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                foreach (var booking in bookings)
                {
                    booking.Status = BookingStatuses.CancelledBySystem;
                    foreach (var pendingPaymentTransaction in pendingTxsByBooking[booking.BookingId])
                    {
                        pendingPaymentTransaction.Status = "Expired";
                    }
                    if (booking.UserId.HasValue)
                    {
                        var userId = booking.UserId.Value;
                        if (booking.FinalAmount > 0 && await HasCompletedBookingPaymentAsync(booking.BookingId))
                        {
                            await _walletService.RefundBalanceAsync(userId, booking.FinalAmount, $"Automatic booking cancellation refund: {request.Reason}");
                        }
                        if (booking.PointsUsed > 0)
                        {
                            await _walletService.RefundSpendablePointsAsync(userId, booking.PointsUsed, $"Automatic booking cancellation point refund: {request.Reason}", booking.BookingId);
                        }
                        await _voucherService.GenerateCompensationVoucherAsync(userId);
                        if (booking.User != null && !string.IsNullOrEmpty(booking.User.Email))
                        {
                            var email = booking.User.Email;
                            var scheduledTime = booking.ScheduledTime;
                            var finalAmount = booking.FinalAmount;
                            var reason = request.Reason;
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _emailService.SendEmailAsync(
                                        email,
                                        "AutoWashPro - Booking Cancellation Notice Due to Incident",
                                        $"Dear Customer,<br/><br/>We regret to inform you that your appointment on {scheduledTime:dd/MM/yyyy HH:mm} has been cancelled due to an unexpected incident.<br/>Reason: {reason}<br/><br/>We have refunded the full amount of <b>{finalAmount:N0} VND</b> and loyalty points (if any) to your wallet.<br/>As an apology, we have also credited a 30,000 VND discount voucher (valid for 7 days) to your account.<br/><br/>Best regards,<br/>AutoWashPro");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Failed to send cancellation email for booking {BookingId}.", booking.BookingId);
                                }
                            });
                        }
                    }
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private Task<bool> HasCompletedBookingPaymentAsync(int bookingId)
        {
            return _context.Transactions.AnyAsync(t =>
                t.ReferenceBookingId == bookingId
                && t.Status == "Completed"
                && (t.TransactionType == "Payment"
                    || t.TransactionType == "BookingPayment"
                    || t.TransactionType == "WalkInPayment"));
        }
        public async Task<BookingResponseDTO> RescheduleBookingAsync(int userId, int bookingId, RescheduleBookingDTO request)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.User)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
            if (booking == null)
            {
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found.");
            }
            if (booking.Status != "Pending" && booking.Status != "Confirmed")
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Can only reschedule bookings in Pending or Confirmed status.");
            }
            var timeRemaining = booking.ScheduledTime - AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            if (timeRemaining.TotalHours < 2)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Bookings can only be rescheduled at least 2 hours before the start time.");
            }
            var newSlot = await _context.TimeSlots.FindAsync(request.NewSlotId);
            if (newSlot == null || newSlot.BranchId != booking.BranchId)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("The new time slot is invalid or does not belong to the same branch.");
            }
            var newTargetDateTime = request.NewScheduledDate.Date.Add(newSlot.StartTime);
            var newSlotEndDateTime = GetSlotEndDateTime(request.NewScheduledDate, newSlot.StartTime, newSlot.EndTime);
            if (newSlotEndDateTime <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Cannot book a time slot in the past.");
            }
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var oldSlot = await _context.TimeSlots
                    .Where(s => s.BranchId == booking.BranchId && s.StartTime <= booking.ScheduledTime.TimeOfDay && s.EndTime >= booking.ScheduledTime.TimeOfDay)
                    .FirstOrDefaultAsync();
                if (oldSlot != null)
                {
                    var oldCapacity = await _context.DailySlotCapacities
                        .FirstOrDefaultAsync(dc => dc.SlotId == oldSlot.SlotId && dc.BranchId == booking.BranchId && dc.Date == booking.ScheduledTime.Date);
                    if (oldCapacity != null)
                    {
                        oldCapacity.BookedWeight -= booking.CapacityWeight;
                        if (oldCapacity.BookedWeight < 0) oldCapacity.BookedWeight = 0;
                    }
                }
                var newCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(dc => dc.SlotId == newSlot.SlotId && dc.BranchId == booking.BranchId && dc.Date == newTargetDateTime.Date);
                if (newCapacity == null)
                {
                    newCapacity = new DailySlotCapacity
                    {
                        SlotId = newSlot.SlotId,
                        BranchId = booking.BranchId,
                        Date = newTargetDateTime.Date,
                        BookedWeight = 0
                    };
                    _context.DailySlotCapacities.Add(newCapacity);
                }
                if (newCapacity.BookedWeight + booking.CapacityWeight > newSlot.MaxCapacity)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("The new time slot does not have enough capacity for your vehicle.");
                }
                newCapacity.BookedWeight += booking.CapacityWeight;
                booking.ScheduledTime = newTargetDateTime;
                booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                if (booking.User != null && !string.IsNullOrEmpty(booking.User.Email))
                {
                    var emailHtml = $"Dear Customer,<br/><br/>Your booking has been rescheduled successfully.<br/>New appointment time: {newTargetDateTime:dd/MM/yyyy HH:mm}.<br/><br/>Best regards,<br/>AutoWashPro";
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(booking.User.Email, "[LuxeWash] Appointment Reschedule Confirmation - #" + booking.BookingId, emailHtml);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Background Task Error - Reschedule Email] Booking #{booking.BookingId}: {ex.Message}");
                        }
                    });
                }
                return new BookingResponseDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate,
                    ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                    ScheduledTime = booking.ScheduledTime,
                    Status = booking.Status,
                    OriginalPrice = booking.OriginalPrice,
                    PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                    AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    FinalAmount = booking.FinalAmount,
                    ProcessingStartTime = booking.ProcessingStartTime.HasValue ? booking.ProcessingStartTime.Value : (DateTime?)null,
                    CompletedTime = booking.CompletedTime.HasValue ? booking.CompletedTime.Value : (DateTime?)null,
                    ActualDurationMinutes = booking.ActualDurationMinutes
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<BookingResponseDTO> AutoCheckInAndStartProcessingAsync(string licensePlate, int branchId, bool autoStart)
        {
            var normalizedPlate = NormalizeLicensePlate(licensePlate);
            if (string.IsNullOrEmpty(normalizedPlate))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Invalid license plate.");
            var matches = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Where(b => b.BranchId == branchId
                    && b.LicensePlate != null
                    && b.LicensePlate == normalizedPlate)
                .ToListAsync();
            if (matches.Count == 0)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"No booking found for vehicle {licensePlate} at this branch.");
            var todayInVN = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.Date;
            var todaysBookings = matches
                .Where(b => b.ScheduledTime.Date == todayInVN &&
                    (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "CheckedIn"))
                .ToList();
            if (todaysBookings.Count == 0)
            {
                var nearestBooking = matches
                    .Where(b => b.Status == "Pending" || b.Status == "Confirmed")
                    .OrderBy(b => Math.Abs((b.ScheduledTime - AutoWashPro.DAL.Helpers.TimeHelper.VnNow).Ticks))
                    .FirstOrDefault();
                if (nearestBooking != null)
                {
                    await BookingCheckInPolicy.ValidateAsync(_context, nearestBooking);
                }
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Vehicle {licensePlate} has no valid booking scheduled for today in pending wash status.");
            }
            var booking = todaysBookings.First();
            if (booking.Status == "Pending" || booking.Status == "Confirmed")
            {
                await BookingCheckInPolicy.ValidateAsync(_context, booking);
            }
            if (booking.Status == "Pending" || booking.Status == "Confirmed")
            {
                if (!await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, booking))
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("BOOKING_PAYMENT_REQUIRED", "BOOKING_PAYMENT_REQUIRED");
                booking.Status = "CheckedIn";
                booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                if (booking.ProcessingLaneId == null)
                {
                    var checkInResult = await _laneCoordinator.CheckInAtEntryGateAsync(
                        booking.LicensePlate ?? "UNKNOWN",
                        booking.BranchId,
                        bookingId: booking.BookingId);
                    if (!checkInResult.IsWaiting && checkInResult.LaneId.HasValue)
                    {
                        booking.ProcessingLaneId = checkInResult.LaneId.Value;
                    }
                }
            }
            if (autoStart && (booking.Status == "CheckedIn" || booking.Status == "Pending" || booking.Status == "Confirmed"))
            {
                if (!await global::BLL.Helpers.PaymentHelper.IsBookingPaidAsync(_context, booking))
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("BOOKING_PAYMENT_REQUIRED", "BOOKING_PAYMENT_REQUIRED");
                if (booking.ProcessingLaneId == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Booking does not have an assigned lane; cannot start processing.");
                booking.Status = "Processing";
                booking.ProcessingStartTime = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                booking.CompletedTime = null;
                booking.ActualDurationMinutes = null;
                booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                string laneName = await _context.Lanes.Where(l => l.LaneId == booking.ProcessingLaneId).Select(l => l.Name).FirstOrDefaultAsync() ?? "";
            }
            await _context.SaveChangesAsync();
            return new BookingResponseDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = normalizedPlate,
                ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                ScheduledTime = booking.ScheduledTime,
                Status = booking.Status,
                OriginalPrice = booking.OriginalPrice,
                PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                FinalAmount = booking.FinalAmount,
                ProcessingStartTime = booking.ProcessingStartTime.HasValue ? booking.ProcessingStartTime.Value : (DateTime?)null,
                CompletedTime = booking.CompletedTime.HasValue ? booking.CompletedTime.Value : (DateTime?)null,
                ActualDurationMinutes = booking.ActualDurationMinutes
            };
        }
        public async Task<int> ProcessOverdueAutomatedWashesAsync()
        {
            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var processingBookings = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                .Include(b => b.Vehicle)
                .Where(b => b.Status == "Processing" && b.ProcessingStartTime.HasValue)
                .ToListAsync();
            int completedCount = 0;
            foreach (var booking in processingBookings)
            {
                int vehicleTypeId = booking.ActualVehicleTypeId ?? booking.Vehicle?.VehicleTypeId ?? 1;
                var serviceIds = booking.BookingDetails.Select(bd => bd.ServiceId).ToList();
                var servicePrices = await _context.ServicePrices
                    .Where(sp => sp.BranchId == booking.BranchId && sp.VehicleTypeId == vehicleTypeId && serviceIds.Contains(sp.ServiceId))
                    .ToListAsync();
                int totalEstimatedMinutes = servicePrices.Sum(sp => sp.EstimatedDurationMinutes);
                if (totalEstimatedMinutes <= 0) totalEstimatedMinutes = 30;
                if (now >= booking.ProcessingStartTime.Value.AddMinutes(totalEstimatedMinutes))
                {
                    booking.Status = "Completed";
                    booking.CompletedTime = now;
                    var duration = (int)Math.Round((booking.CompletedTime.Value - booking.ProcessingStartTime.Value).TotalMinutes);
                    booking.ActualDurationMinutes = duration < 1 ? 1 : duration;
                    booking.UpdatedAt = now;
                    await _bookingMaterialUsageService.ConsumeForCompletedBookingAsync(booking.BookingId);
                    if (booking.UserId > 0)
                    {
                        var userProfile = await _context.CustomerProfiles
                            .Include(cp => cp.Tier)
                            .FirstOrDefaultAsync(cp => cp.UserId == booking.UserId);
                        if (userProfile?.Tier != null && booking.FinalAmount > 0)
                        {
                            int pointsEarned = (int)((booking.FinalAmount / PointConstants.VndPerEarnedPoint) * (decimal)userProfile.Tier.PointMultiplier);
                            if (pointsEarned > 0)
                            {
                                await _walletService.AwardCompletionPointsAsync(booking.UserId.Value, pointsEarned, booking.BookingId);
                            }
                        }
                        if (userProfile != null)
                        {
                            userProfile.LastVisitDate = now;
                        }
                    }
                    completedCount++;
                }
            }
            if (completedCount > 0)
            {
                await _context.SaveChangesAsync();
            }
            return completedCount;
        }
        public async Task<BookingResponseDTO> AcceptRelocationAsync(int userId, int bookingId, AcceptRelocationRequestDTO request)
        {
            var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                .Include(b => b.BookingDetails)
                    .ThenInclude(d => d.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
            if (booking == null)
            {
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found or does not belong to the user.");
            }
            if (booking.ScheduledTime <= AutoWashPro.DAL.Helpers.TimeHelper.VnNow)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("This relocation proposal has expired because the scheduled time has passed.");
            }
            if (booking.Status != "Pending")
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Cannot relocate booking in status: {booking.Status}");
            }
            var alternativeBranch = await _context.Branches.FindAsync(request.AlternativeBranchId);
            if (alternativeBranch == null || !alternativeBranch.IsActive)
            {
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Alternative branch not found or inactive.");
            }
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == request.VoucherCode && v.BranchId == request.AlternativeBranchId && v.IsActive);
            if (voucher == null || voucher.ApprovalStatus != "Approved")
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher is invalid or not approved.");
            }
            var originalSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(ts => ts.BranchId == booking.BranchId && booking.ScheduledTime.TimeOfDay >= ts.StartTime && booking.ScheduledTime.TimeOfDay <= ts.EndTime);
            if (originalSlot != null)
            {
                var originalCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(c => c.BranchId == booking.BranchId && c.Date == booking.ScheduledTime.Date && c.SlotId == originalSlot.SlotId);
                if (originalCapacity != null)
                {
                    originalCapacity.BookedWeight = Math.Max(0, originalCapacity.BookedWeight - booking.CapacityWeight);
                }
            }
            var alternativeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(ts => ts.BranchId == request.AlternativeBranchId && booking.ScheduledTime.TimeOfDay >= ts.StartTime && booking.ScheduledTime.TimeOfDay <= ts.EndTime);
            if (alternativeSlot != null)
            {
                var newCapacity = await _context.DailySlotCapacities
                    .FirstOrDefaultAsync(c => c.BranchId == request.AlternativeBranchId && c.Date == booking.ScheduledTime.Date && c.SlotId == alternativeSlot.SlotId);
                if (newCapacity == null)
                {
                    if (booking.CapacityWeight > alternativeSlot.MaxCapacity)
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException("The alternative branch is fully booked for this time slot.");
                    newCapacity = new DAL.Entities.DailySlotCapacity
                    {
                        BranchId = request.AlternativeBranchId,
                        Date = booking.ScheduledTime.Date,
                        SlotId = alternativeSlot.SlotId,
                        BookedWeight = booking.CapacityWeight
                    };
                    _context.DailySlotCapacities.Add(newCapacity);
                }
                else
                {
                    if (newCapacity.BookedWeight + booking.CapacityWeight > alternativeSlot.MaxCapacity)
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException("The alternative branch is fully booked for this time slot.");
                    newCapacity.BookedWeight += booking.CapacityWeight;
                }
            }
            booking.BranchId = request.AlternativeBranchId;
            booking.AppliedVoucherId = voucher.VoucherId;
            decimal discount = voucher.DiscountAmount;
            booking.VoucherDiscountAmount = discount;
            booking.FinalAmount = Math.Max(0, booking.OriginalPrice - booking.PointDiscountAmount - booking.VoucherDiscountAmount);
            booking.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            await _context.SaveChangesAsync();
            var serviceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList();
            return new BookingResponseDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = booking.LicensePlate,
                ServiceNames = serviceNames,
                ScheduledTime = booking.ScheduledTime,
                Status = booking.Status,
                OriginalPrice = booking.OriginalPrice,
                PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                FinalAmount = booking.FinalAmount
            };
        }
        public async Task<OverloadSuggestionResponseDTO?> GetPendingOverloadSuggestionAsync(int userId, int bookingId)
        {
            var nowUtc = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            var nowVn = nowUtc;
            var suggestion = await _context.OverloadSuggestions
                .Include(s => s.Booking)
                .Where(s => s.BookingId == bookingId 
                         && s.Booking.UserId == userId 
                         && !s.IsProcessed 
                         && s.ExpiresAt > nowUtc 
                         && s.Booking.Status == "Pending" 
                         && s.Booking.ScheduledTime > nowVn)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
            if (suggestion == null) return null;
            return new OverloadSuggestionResponseDTO
            {
                SuggestionId = suggestion.Id,
                BookingId = suggestion.BookingId,
                SuggestedBranchId = suggestion.SuggestedBranchId,
                SuggestedBranchName = suggestion.SuggestedBranchName,
                SuggestedSlotId = suggestion.SuggestedSlotId,
                SuggestedTime = new DateTimeOffset(DateTime.SpecifyKind(suggestion.SuggestedTime, DateTimeKind.Unspecified), TimeSpan.FromHours(7)),
                ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(suggestion.ExpiresAt, DateTimeKind.Utc))
            };
        }
        public async Task<HandleOverloadDecisionResponseDTO> HandleOverloadDecisionAsync(int userId, int bookingId, HandleOverloadDecisionDTO request)
        {
            int retryCount = 3;
            while (retryCount > 0)
            {
                try
                {
                    return await HandleOverloadDecisionInnerAsync(userId, bookingId, request);
                }
                catch (DbUpdateConcurrencyException)
                {
                    retryCount--;
                    if (retryCount == 0) throw new AutoWashPro.BLL.Exceptions.ConflictException("Data was modified by another transaction. Please reload and try again.", "CONCURRENCY_CONFLICT");
                    await Task.Delay(50);
                }
                catch (MySqlConnector.MySqlException ex) when (ex.Number == 1213 || ex.Number == 1205)
                {
                    retryCount--;
                    if (retryCount == 0) throw new AutoWashPro.BLL.Exceptions.ConflictException("Data was modified by another transaction. Please reload and try again.", "CONCURRENCY_CONFLICT");
                    await Task.Delay(50);
                }
            }
            throw new AutoWashPro.BLL.Exceptions.ConflictException("Data was modified by another transaction. Please reload and try again.", "CONCURRENCY_CONFLICT");
        }
        private async Task<HandleOverloadDecisionResponseDTO> HandleOverloadDecisionInnerAsync(int userId, int bookingId, HandleOverloadDecisionDTO request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            string? switchNotificationTitle = null;
            string? switchNotificationBody = null;
            try
            {
                var booking = await _context.Bookings.Include(b => b.AppliedVoucher)
                    .Include(b => b.Branch)
                    .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
                if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Booking not found");
                if (booking.Status != "Pending") throw new AutoWashPro.BLL.Exceptions.ConflictException("Booking is not pending.", "BOOKING_NOT_PENDING");
                var nowUtc = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                var nowVn = nowUtc;
                if (booking.ScheduledTime <= nowVn) throw new AutoWashPro.BLL.Exceptions.ConflictException("Booking scheduled time has already passed.", "BOOKING_TIME_PASSED");
                var decision = request.Decision?.Trim();
                if (!string.Equals(decision, "Switch", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(decision, "Cancel", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(decision, "Keep", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Decision must be Switch, Cancel, or Keep.");
                }
                decision = char.ToUpper(decision![0]) + decision.Substring(1).ToLower();
                var suggestion = await _context.OverloadSuggestions
                    .Where(s => s.BookingId == bookingId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
                if (suggestion == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("No overload suggestion found for this booking.", "OVERLOAD_SUGGESTION_NOT_FOUND");
                if (suggestion.IsProcessed) throw new AutoWashPro.BLL.Exceptions.ConflictException("The overload suggestion has already been processed.", "OVERLOAD_SUGGESTION_ALREADY_PROCESSED");
                if (suggestion.ExpiresAt < nowUtc) throw new AutoWashPro.BLL.Exceptions.ConflictException("The overload suggestion has expired.", "OVERLOAD_SUGGESTION_EXPIRED");
                var response = new HandleOverloadDecisionResponseDTO 
                { 
                    Success = true,
                    Decision = decision
                };
                if (decision == "Keep")
                {
                    booking.IsWaitAccepted = true;
                    suggestion.IsProcessed = true;
                    await _context.SaveChangesAsync();
                    response.Message = "You have chosen to keep your current booking and wait.";
                }
                else if (decision == "Cancel")
                {
                    var oldSlot = await _context.DailySlotCapacities
                        .Include(c => c.TimeSlot)
                        .FirstOrDefaultAsync(c => c.BranchId == booking.BranchId && c.Date == booking.ScheduledTime.Date && c.TimeSlot.StartTime <= booking.ScheduledTime.TimeOfDay && c.TimeSlot.EndTime > booking.ScheduledTime.TimeOfDay);
                    if (oldSlot != null)
                    {
                        oldSlot.BookedWeight = Math.Max(0, oldSlot.BookedWeight - (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1));
                    }
                    booking.Status = "Cancelled";
                    suggestion.IsProcessed = true;
                    response.Refund = new OverloadRefundDTO
                    {
                        RefundedAmount = 0,
                        RefundDestination = null,
                        RefundedPoints = 0,
                        RestoredVoucherId = null
                    };
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
                    bool paymentCompleted = await _context.Transactions.AnyAsync(t => t.ReferenceBookingId == booking.BookingId && (t.TransactionType == "WalkInPayment" || t.TransactionType == "Payment" || t.TransactionType == "BookingPayment") && t.Status == "Completed");
                    bool refundExists = await _context.Transactions.AnyAsync(t => t.ReferenceBookingId == booking.BookingId && t.TransactionType == "Refund");
                    if (wallet != null && booking.FinalAmount > 0 && paymentCompleted && !refundExists)
                    {
                        wallet.Balance += booking.FinalAmount;
                        var refundTx = new Transaction
                        {
                            WalletId = wallet.WalletId,
                            Amount = booking.FinalAmount,
                            TransactionType = "Refund",
                            Description = $"Refund for overload cancelled booking #{booking.BookingId}",
                            ReferenceBookingId = booking.BookingId
                        };
                        _context.Transactions.Add(refundTx);
                        response.Refund.RefundedAmount = booking.FinalAmount;
                        response.Refund.RefundDestination = "Wallet";
                    }
                    if (booking.PointsUsed > 0)
                    {
                        await _walletService.RefundSpendablePointsAsync(
                            userId,
                            booking.PointsUsed,
                            $"Points refunded for overload-cancelled booking #{booking.BookingId}"
                        );
                        response.Refund.RefundedPoints = booking.PointsUsed;
                    }
                    if (booking.AppliedVoucherId.HasValue)
                    {
                        var userVoucher = await _context.UserVouchers
                            .Include(uv => uv.Voucher)
                            .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == booking.AppliedVoucherId.Value);
                        if (userVoucher != null)
                        {
                            if (userVoucher.UsageCount > 0)
                            {
                                userVoucher.UsageCount -= 1;
                            }
                            userVoucher.IsUsed = false;
                            userVoucher.UsedDate = userVoucher.UsageCount > 0 ? userVoucher.UsedDate : null;
                            userVoucher.LastUsedDate = userVoucher.UsageCount > 0 ? userVoucher.LastUsedDate : null;
                            if (userVoucher.Voucher.CurrentUsageCount > 0)
                            {
                                userVoucher.Voucher.CurrentUsageCount -= 1;
                            }
                            response.Refund.RestoredVoucherId = booking.AppliedVoucherId.Value;
                        }
                    }
                    await _context.SaveChangesAsync();
                    response.Message = "Booking cancelled due to overload. Penalty waived. Refunds processed.";
                }
                else if (decision == "Switch")
                {
                    var originalBranchName = booking.Branch?.Name ?? $"Chi nhánh #{booking.BranchId}";
                    var oldSlot = await _context.DailySlotCapacities
                        .Include(c => c.TimeSlot)
                        .FirstOrDefaultAsync(c => c.BranchId == booking.BranchId && c.Date == booking.ScheduledTime.Date && c.TimeSlot.StartTime <= booking.ScheduledTime.TimeOfDay && c.TimeSlot.EndTime > booking.ScheduledTime.TimeOfDay);
                    if (oldSlot != null)
                    {
                        oldSlot.BookedWeight = Math.Max(0, oldSlot.BookedWeight - (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1));
                    }
                    var destBranch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == suggestion.SuggestedBranchId && b.IsActive);
                    if (destBranch == null) throw new AutoWashPro.BLL.Exceptions.ConflictException("The destination branch is no longer active.", "DESTINATION_BRANCH_INACTIVE");
                    var newSlot = await _context.DailySlotCapacities
                        .Include(c => c.TimeSlot)
                        .FirstOrDefaultAsync(c => c.SlotId == suggestion.SuggestedSlotId 
                                               && c.BranchId == suggestion.SuggestedBranchId 
                                               && c.Date == suggestion.SuggestedTime.Date);
                    if (newSlot == null || newSlot.BookedWeight + (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1) > newSlot.TimeSlot.MaxCapacity)
                    {
                        throw new AutoWashPro.BLL.Exceptions.ConflictException("Target slot is full or unavailable.", "TARGET_SLOT_FULL");
                    }
                    newSlot.BookedWeight += (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1);
                    booking.BranchId = suggestion.SuggestedBranchId;
                    booking.Branch = destBranch;
                    booking.ScheduledTime = suggestion.SuggestedTime;
                    booking.IsWaitAccepted = true;
                    suggestion.IsProcessed = true;
                    decimal discountValue = booking.OriginalPrice > 0 ? booking.OriginalPrice * 0.10m : 50000m;
                    var voucher = new Voucher
                    {
                        Code = $"OVL-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}",
                        DiscountAmount = discountValue,
                        MaxUsages = 1,
                        MaxUsagePerUser = 1,
                        ExpiryDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow.AddMonths(1),
                        IsActive = true,
                        VoucherType = AutoWashPro.DAL.Enums.VoucherType.Discount,
                        CampaignType = AutoWashPro.DAL.Enums.VoucherCampaignType.Manual,
                        ProposalNote = "Overload compensation voucher 10%"
                    };
                    _context.Vouchers.Add(voucher);
                    await _context.SaveChangesAsync();
                    var userVoucher = new UserVoucher
                    {
                        UserId = userId,
                        VoucherId = voucher.VoucherId,
                        ExpiryDate = voucher.ExpiryDate,
                        ReceivedDate = AutoWashPro.DAL.Helpers.TimeHelper.VnNow
                    };
                    _context.UserVouchers.Add(userVoucher);
                    await _context.SaveChangesAsync();
                    response.Message = "Switched to new branch successfully. You received a compensation voucher.";
                    response.Voucher = new VoucherResponseDTO
                    {
                        VoucherId = voucher.VoucherId,
                        Code = voucher.Code,
                        DiscountAmount = voucher.DiscountAmount,
                        ExpiryDate = voucher.ExpiryDate,
                        IsActive = voucher.IsActive
                    };
                    switchNotificationTitle = "Đổi chi nhánh thành công";
                    switchNotificationBody =
                        $"Lịch hẹn #{booking.BookingId} đã được đổi từ '{originalBranchName}' " +
                        $"sang '{destBranch.Name}' vào lúc {booking.ScheduledTime:dd/MM/yyyy HH:mm}. " +
                        $"Voucher đền bù {voucher.Code} đã được thêm vào tài khoản.";
                }
                else
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Decision must be Switch, Cancel, or Keep.");
                }
                await transaction.CommitAsync();
                if (switchNotificationTitle != null && switchNotificationBody != null)
                {
                    try
                    {
                        await _userNotificationService.CreateNotificationAsync(
                            userId,
                            switchNotificationTitle,
                            switchNotificationBody,
                            "Booking",
                            booking.BookingId.ToString());
                    }
                    catch (Exception ex)
                    {
                        // The booking switch is already committed. A notification failure must
                        // not turn a successful customer decision into a failed API response.
                        _logger.LogError(
                            ex,
                            "Failed to send branch-switch confirmation for Booking {BookingId} and User {UserId}.",
                            booking.BookingId,
                            userId);
                    }
                }
                response.UpdatedBooking = new BookingResponseDTO
                {
                    BookingId = booking.BookingId,
                    BranchId = booking.BranchId,
                    BranchName = booking.Branch?.Name ?? "",
                    IsWaitAccepted = booking.IsWaitAccepted,
                    HasPendingOverloadSuggestion = false,
                    LicensePlate = booking.LicensePlate ?? "",
                    ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = booking.ScheduledTime,
                    Status = booking.Status,
                    OriginalPrice = booking.OriginalPrice,
                    AppliedVoucherId = booking.AppliedVoucherId,
                    AppliedVoucherCode = booking.AppliedVoucher != null ? booking.AppliedVoucher.Code : string.Empty,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    PointsUsed = booking.PointsUsed,
                    PointDiscountAmount = booking.PointDiscountAmount,
                    FinalAmount = booking.FinalAmount
                };
                return response;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private static long GeneratePayOsOrderCode()
        {
            var timestampPart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000_000;
            var randomPart = Random.Shared.Next(10, 99);
            return timestampPart * 100 + randomPart;
        }
        private static int ToPayOsAmount(decimal amount)
        {
            if (amount <= 0)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("PayOS payment amount must be greater than 0.");
            if (amount != decimal.Truncate(amount))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("PayOS only supports whole VND amounts with no decimal part.");
            if (amount > int.MaxValue)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("PayOS payment amount exceeds supported limit.");
            return decimal.ToInt32(amount);
        }
    }
}
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
