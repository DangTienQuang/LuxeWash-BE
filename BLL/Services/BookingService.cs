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

        public BookingService(
            AutoWashDbContext context,
            IWalletService walletService,
            ITierService tierService,
            IEmailService emailService,
            IVoucherService voucherService,
            IVoucherCampaignService voucherCampaignService,
            IPayOsService payOsService,
            IBookingMaterialUsageService bookingMaterialUsageService)
        {
            _context = context;
            _walletService = walletService;
            _tierService = tierService;
            _emailService = emailService;
            _voucherService = voucherService;
            _voucherCampaignService = voucherCampaignService;
            _payOsService = payOsService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
        }

        public async Task<List<TimeSlotResponseDTO>> GetAvailableSlotsAsync(int userId, CheckAvailableSlotsRequestDTO request)
        {
            var userProfile = await _context.CustomerProfiles.Include(cp => cp.Tier).FirstOrDefaultAsync(cp => cp.UserId == userId);
            if (userProfile == null || userProfile.Tier == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy thông tin hạng thành viên.");

            // 1. SỬA LỖI MÚI GIỜ (Dùng cho cả Docker/Linux/Windows)
            TimeZoneInfo vnTimeZone;
            try { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch (TimeZoneNotFoundException) { vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }

            DateTime todayInVN = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone).Date;
            TimeSpan currentTimeInVN = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone).TimeOfDay;

            var maxDate = todayInVN.AddDays(userProfile.Tier.BookingWindowDays);

            if (request.TargetDate.Date < todayInVN || request.TargetDate.Date > maxDate)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Hạng {userProfile.Tier.TierName} chỉ được đặt trước từ hôm nay đến ngày {maxDate:dd/MM/yyyy}.");
            }

            // 2. TÍNH TỔNG TRỌNG LƯỢNG (WEIGHT) CỦA GIỎ HÀNG KHÁCH VỪA CHỌN
            int totalRequestWeight = 0;
            if (request.ServiceIds != null && request.ServiceIds.Any())
            {
                var vehicleType = await _context.VehicleTypes.FirstOrDefaultAsync(vt => vt.Id == request.VehicleTypeId);
                var baseWeight = vehicleType?.BaseWeight ?? 0;

                foreach (var serviceId in request.ServiceIds)
                {
                    var servicePrice = await _context.ServicePrices
                        .FirstOrDefaultAsync(sp => sp.ServiceId == serviceId
                                                && sp.VehicleTypeId == request.VehicleTypeId
                                                && sp.BranchId == request.BranchId);

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

            bool isVip = userProfile.Tier.TierName.ToLower() == "gold" || userProfile.Tier.TierName.ToLower() == "platinum";

            // 3. VÒNG LẶP KIỂM TRA TỪNG SLOT
            foreach (var slot in allSlots)
            {
                var slotDto = new TimeSlotResponseDTO
                {
                    SlotId = slot.SlotId,
                    TimeRange = $"{slot.StartTime:hh\\:mm} - {slot.EndTime:hh\\:mm}",
                    IsAvailable = true,
                    Reason = "Trống"
                };

                if (slot.IsVipOnly && !isVip)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = "Chỉ dành cho VIP";
                }

                // Chặn slot quá giờ so với giờ Việt Nam
                if (request.TargetDate.Date == todayInVN && slot.StartTime < currentTimeInVN)
                {
                    slotDto.IsAvailable = false;
                    slotDto.Reason = "Đã qua giờ";
                }

                int bookedWeight = dailyCapacities.TryGetValue(slot.SlotId, out int weight) ? weight : 0;

                // --- LOGIC AI SỨC CHỨA ---
                // Lượng đã đặt + Lượng khách ĐANG ĐỊNH ĐẶT > Sức chứa tối đa
                if (bookedWeight + totalRequestWeight > slot.MaxCapacity)
                {
                    slotDto.IsAvailable = false;
                    // Nếu khách có add xe vào giỏ thì báo "Không đủ chỗ cho dịch vụ", nếu không thì báo "Đã kín"
                    slotDto.Reason = totalRequestWeight > 0 ? "Không đủ sức chứa cho giỏ hàng của bạn" : "Đã kín chỗ";
                }

                response.Add(slotDto);
            }

            return response;
        }

        public async Task<List<AdminBookingResponseDTO>> GetAllBookingsByDateAsync(DateTime targetDate)
        {
            var bookings = await _context.Bookings
                .Where(b => b.ScheduledTime.Date == targetDate.Date)
                .OrderBy(b => b.ScheduledTime)
                .Select(b => new AdminBookingResponseDTO
                {
                    BookingId = b.BookingId,
                    LicensePlate = b.LicensePlate ?? "",
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointDiscountAmount = b.PointDiscountAmount,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount
                })
                .ToListAsync();

            var paymentStatuses = await GetPaymentStatusesByBookingIdsAsync(bookings.Select(b => b.BookingId));
            foreach (var booking in bookings)
            {
                booking.PaymentStatus = GetPaymentStatus(paymentStatuses, booking.BookingId);
            }

            return bookings;
        }

        public async Task<BookingResponseDTO> GetBookingByIdAsync(int userId, int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy chi tiết lịch hẹn hoặc bạn không có quyền xem.");

            return new BookingResponseDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = booking.LicensePlate,
                ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                ScheduledTime = booking.ScheduledTime,
                Status = booking.Status,
                OriginalPrice = booking.OriginalPrice,
                PointDiscountAmount = booking.PointDiscountAmount,
                VoucherDiscountAmount = booking.VoucherDiscountAmount,
                FinalAmount = booking.FinalAmount
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
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Biển số xe không hợp lệ.");

            var todayInVN = DateTime.UtcNow.ToVnTime().Date;

            // Step 1: Query Bookings for today at the specific branch
            var preBooked = await _context.Bookings
                .Where(b => (b.LicensePlate ?? "").Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedPlate
                         && b.BranchId == branchId
                         && b.ScheduledTime.Date == todayInVN
                         && (b.Status == "Pending" || b.Status == "Confirmed"))
                .OrderBy(b => b.ScheduledTime)
                .Select(b => new AdminBookingResponseDTO
                {
                    BookingId = b.BookingId,
                    LicensePlate = b.LicensePlate ?? "",
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointDiscountAmount = b.PointDiscountAmount,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (preBooked != null)
            {
                var paymentStatuses = await GetPaymentStatusesByBookingIdsAsync(new List<int> { preBooked.BookingId });
                preBooked.PaymentStatus = GetPaymentStatus(paymentStatuses, preBooked.BookingId);

                return new SmartLicensePlateResponseDTO
                {
                    CustomerType = "PreBooked",
                    Data = preBooked
                };
            }

            // Step 2: Query FleetVehicles (Global)
            var fleetVehicle = await _context.FleetVehicles
                .Include(fv => fv.BusinessProfile)
                .Include(fv => fv.VehicleType)
                .Where(fv => (fv.LicensePlate ?? "").Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedPlate
                          && (fv.Status == "Approved" || fv.Status == "Active")
                          && fv.BusinessProfile != null
                          && fv.BusinessProfile.ApprovalStatus == "Approved"
                          && fv.BusinessProfile.IsContractActive)
                .Select(fv => new global::BLL.DTOs.Fleet.FleetVehicleDTO
                {
                    FleetVehicleId = fv.FleetVehicleId,
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

            // Step 3: WalkIn
            var registeredVehicle = await _context.Vehicles
                .Include(v => v.User)
                .ThenInclude(u => u.CustomerProfile)
                .Where(v => v.LicensePlate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedPlate && !v.IsDeleted && v.UserId != null)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (registeredVehicle != null && registeredVehicle.User != null)
            {
                return new SmartLicensePlateResponseDTO
                {
                    CustomerType = "WalkIn",
                    Data = new
                    {
                        userId = registeredVehicle.UserId,
                        customerName = registeredVehicle.User.CustomerProfile?.FullName,
                        phoneNumber = registeredVehicle.User.PhoneNumber,
                        vehicleId = registeredVehicle.Id
                    }
                };
            }

            return new SmartLicensePlateResponseDTO
            {
                CustomerType = "WalkIn",
                Data = null
            };
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
                        || EF.Property<string?>(t, nameof(Transaction.TransactionType)) == "WalkInPayment"))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    BookingId = t.ReferenceBookingId!.Value,
                    Status = EF.Property<string?>(t, nameof(Transaction.Status)) ?? "",
                    t.CreatedAt
                })
                .ToListAsync();

            return paymentTransactions
                .GroupBy(t => t.BookingId)
                .ToDictionary(g => g.Key, g => g.First().Status);
        }

        private static string GetPaymentStatus(Dictionary<int, string> paymentStatuses, int bookingId)
        {
            return paymentStatuses.TryGetValue(bookingId, out var paymentStatus)
                && string.Equals(paymentStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                    ? "Completed"
                    : "Unpaid";
        }

        public async Task<BookingResponseDTO> UpdateBookingStatusByLicensePlateAsync(string licensePlate, string newStatus)
        {
            // 1. KIỂM TRA DỮ LIỆU ĐẦU VÀO
            var allowedStatuses = new[] { "Pending", "CheckedIn", "Completed", "Cancelled", "Delayed", "CancelledBySystem" };
            if (!allowedStatuses.Contains(newStatus))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Trạng thái không hợp lệ.");

            var normalizedPlate = NormalizeLicensePlate(licensePlate);
            if (string.IsNullOrEmpty(normalizedPlate))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Biển số xe không hợp lệ.");

            // 2. TÌM KIẾM DIỆN RỘNG (Tránh hoàn toàn lỗi lệch giờ UTC vs VN)
            // Lấy dư ra 24h trước và sau để đảm bảo không bỏ sót bất kỳ đơn nào do lệch timezone
            var startTime = DateTime.UtcNow.AddHours(-24);
            var endTime = DateTime.UtcNow.AddHours(24);

            var query = await _context.Bookings
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Service)
                .Where(b => b.ScheduledTime >= startTime && b.ScheduledTime <= endTime)
                .ToListAsync();

            // 3. LỌC CHÍNH XÁC BIỂN SỐ (In-memory)
            var matches = query.Where(b =>
                NormalizeLicensePlate(b.LicensePlate) == normalizedPlate
            ).ToList();

            if (matches.Count == 0)
            {
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Không tìm thấy lịch hẹn nào cho xe {licensePlate} trong khoảng thời gian gần đây.");
            }

            // 4. LỌC CHÍNH XÁC NGÀY HÔM NAY (Giờ Việt Nam)
            var todayInVN = DateTime.UtcNow.ToVnTime().Date;
            var todaysBookings = matches.Where(b => b.ScheduledTime.ToVnTime().Date == todayInVN).ToList();

            if (todaysBookings.Count == 0)
            {
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Xe {licensePlate} có lịch, nhưng không phải là lịch của ngày hôm nay ({todayInVN:dd/MM/yyyy}).");
            }

            if (todaysBookings.Count > 1)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Phát hiện nhiều lịch hẹn ({todaysBookings.Count}) cho xe {licensePlate} trong ngày hôm nay. Vui lòng sử dụng mã Booking ID để thao tác.");
            }

            var booking = todaysBookings.First();

            // 5. KIỂM TRA LOGIC CHUYỂN TRẠNG THÁI (Báo lỗi 400 rõ ràng thay vì 404 Not Found)
            if (booking.Status == newStatus)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Trạng thái hiện tại của đơn hàng đã là '{newStatus}' rồi.");
            }

            bool isStatusValid = false;
            if (newStatus == "CheckedIn" && booking.Status == "Pending") isStatusValid = true;
            else if (newStatus == "Completed" && booking.Status == "CheckedIn") isStatusValid = true;
            else if ((newStatus == "Cancelled" || newStatus == "Delayed") && (booking.Status == "Pending" || booking.Status == "CheckedIn")) isStatusValid = true;

            if (!isStatusValid)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Không thể chuyển trạng thái từ '{booking.Status}' sang '{newStatus}'.");
            }

            // 6. THỰC THI CẬP NHẬT (Tận dụng logic của hàm UpdateBookingStatusAsync)
            var isUpdated = await UpdateBookingStatusAsync(booking.BookingId, newStatus);

            if (!isUpdated)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Cập nhật trạng thái thất bại do hệ thống.");
            }

            // 7. TRẢ VỀ DTO
            return new BookingResponseDTO
            {
                BookingId = booking.BookingId,
                LicensePlate = normalizedPlate,
                ServiceNames = booking.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                ScheduledTime = booking.ScheduledTime,
                Status = newStatus,
                OriginalPrice = booking.OriginalPrice,
                PointDiscountAmount = booking.PointDiscountAmount,
                VoucherDiscountAmount = booking.VoucherDiscountAmount,
                FinalAmount = booking.FinalAmount
            };
        }
        public async Task<bool> UpdateBookingStatusAsync(int bookingId, string newStatus)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy lịch hẹn.");

            var allowedStatuses = new[] { "Pending", "CheckedIn", "Completed", "Cancelled", "Delayed", "CancelledBySystem" };
            if (!allowedStatuses.Contains(newStatus)) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Trạng thái không hợp lệ.");

            if ((newStatus == "CheckedIn" || newStatus == "Completed")
                && booking.FinalAmount > 0
                && !await HasCompletedBookingPaymentAsync(booking.BookingId))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Lịch hẹn chưa thanh toán, không thể check-in hoặc hoàn thành.");

            var isCompletingNow = newStatus == "Completed" && booking.Status != "Completed";

            booking.Status = newStatus;
            booking.UpdatedAt = DateTime.UtcNow;

            if (newStatus == "Completed")
            {
                await _bookingMaterialUsageService.ConsumeForCompletedBookingAsync(booking.BookingId);
            }

            if (isCompletingNow)
            {
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
                        userProfile.LastVisitDate = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            if (isCompletingNow && booking.UserId.HasValue)
            {
                await _voucherCampaignService.ProcessMilestoneCampaignsAsync(booking.UserId.Value);
            }

            return true;
        }

        public async Task<CompatibilityDTO> ValidateBookingCompatibilityAsync(int userId, int branchId, int slotId, DateTime targetDate, int? vehicleId, string licensePlate, List<int> serviceIds)
        {
            if (serviceIds == null || serviceIds.Count == 0)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Vui lòng chọn ít nhất 1 dịch vụ.");

            var slot = await _context.TimeSlots.FirstOrDefaultAsync(ts => ts.SlotId == slotId && ts.BranchId == branchId);
            if (slot == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Khung giờ không hợp lệ.");

            if (slot.IsVipOnly)
            {
                var profile = await _context.CustomerProfiles
                    .Include(p => p.Tier)
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                if (profile == null || profile.Tier == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Không tìm thấy thông tin hạng thành viên.");

                string tierName = profile.Tier.TierName.ToLower();
                if (tierName != "gold" && tierName != "platinum")
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Khung giờ này là đặc quyền chỉ dành riêng cho thành viên Gold và Platinum.");
                }
            }

            var targetDateTime = targetDate.Date.Add(slot.StartTime);
            if (targetDateTime < DateTime.UtcNow)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Không thể đặt lịch trong quá khứ.");

            int totalCapacityWeight = 0;

            var vehicle = await _context.Vehicles.Include(v => v.VehicleType).FirstOrDefaultAsync(v => v.LicensePlate == licensePlate && v.UserId == userId && !v.IsDeleted);
            if (vehicle == null)
                throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Xe với biển số {licensePlate} không tồn tại trong hồ sơ của bạn.");

            bool hasActiveBooking = await _context.Bookings.AnyAsync(b => b.LicensePlate == licensePlate && (b.Status == "Pending" || b.Status == "CheckedIn"));
            if (hasActiveBooking)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Xe biển số {licensePlate} đang có lịch hẹn chưa hoàn thành. Không thể đặt thêm lịch mới cho xe này.");

            foreach (var serviceId in serviceIds)
            {
                var service = await _context.Services.FindAsync(serviceId);
                if (service == null || !service.IsActive)
                    throw new AutoWashPro.BLL.Exceptions.NotFoundException($"Dịch vụ {serviceId} không tồn tại hoặc đã ngừng kinh doanh.");

                var servicePrice = await _context.ServicePrices.FirstOrDefaultAsync(sp => sp.ServiceId == serviceId && sp.VehicleTypeId == vehicle.VehicleTypeId && sp.BranchId == branchId);
                if (servicePrice == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Dịch vụ {serviceId} chưa hỗ trợ cho loại xe này.");

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
                    Message = "Xưởng không đủ sức chứa cho yêu cầu của bạn.",
                    RemainingCapacity = remainingCapacity > 0 ? remainingCapacity : 0,
                    TotalCapacityWeight = totalCapacityWeight,
                    MaxCapacityOfSlot = slot.MaxCapacity
                };
            }

            return new CompatibilityDTO
            {
                IsCompatible = true,
                Message = "Sức chứa hợp lệ.",
                RemainingCapacity = remainingCapacity,
                TotalCapacityWeight = totalCapacityWeight,
                MaxCapacityOfSlot = slot.MaxCapacity
            };
        }
        // File: BLL/Services/BookingService.cs

        public async Task<bool> SendBookingConfirmationEmailAsync(int userId, int bookingId)
        {
            try
            {
                // 1. Lấy thông tin Booking và User. 
                // Phải Include đầy đủ vì context này chạy hoàn toàn độc lập với luồng CreateBooking
                var booking = await _context.Bookings
                    .Include(b => b.BookingDetails)
                        .ThenInclude(bd => bd.Service)
                    .Include(b => b.User)
                        .ThenInclude(u => u.CustomerProfile)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

                // 2. Validate an toàn
                if (booking == null || string.IsNullOrEmpty(booking.User?.Email))
                {
                    Console.WriteLine($"[Cảnh báo] Không thể gửi mail: Không tìm thấy đơn #{bookingId} hoặc User không có email.");
                    return false;
                }

                var customerName = booking.User.CustomerProfile?.FullName ?? "Quý khách";

                // 3. Build HTML Template (Nghiệp vụ tốn CPU)
                var emailHtml = EmailTemplateBuilder.BuildBookingConfirmationEmail(
                    booking,
                    booking.BookingDetails.ToList(),
                    customerName
                );

                // 4. Gọi Service Gửi mail
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    $"[SmartWash] Đặt lịch thành công - #{booking.BookingId}",
                    emailHtml
                );

                return true;
            }
            catch (Exception ex)
            {
                // Trong Background Task, KHÔNG ĐƯỢC throw exception làm crash app. 
                // Chỉ ghi Log để Developer trace lỗi.
                Console.WriteLine($"[Lỗi Background Task - Email] Booking #{bookingId}: {ex.Message}");
                return false;
            }
        }
        public async Task<CompatibilityDTO> CheckCompatibilityAsync(int userId, CheckCompatibilityRequestDTO request)
        {
            return await ValidateBookingCompatibilityAsync(userId, request.BranchId, request.SlotId, request.TargetDate, request.VehicleId, request.LicensePlate, request.ServiceIds);
        }

        public async Task<BookingResponseDTO> CreateBookingAsync(int userId, CreateBookingDTO request)
        {
            var compatibility = await ValidateBookingCompatibilityAsync(userId, request.BranchId, request.SlotId, request.ScheduledDate, request.VehicleId, request.LicensePlate, request.ServiceIds);

            if (!compatibility.IsCompatible)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException(compatibility.Message ?? "Xưởng không đủ sức chứa cho yêu cầu của bạn.");
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
                 throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy phương tiện.");
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
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Dịch vụ {serviceId} không hỗ trợ cho loại xe này.");

                var actualWeight = sp.CapacityWeight > 0 ? sp.CapacityWeight : vehicleTypeQuery.BaseWeight;
                if (actualWeight > maxCapacityWeight) maxCapacityWeight = actualWeight;

                totalOriginalPrice += sp.Price;

                pendingDetails.Add(new BookingDetail
                {
                    ServiceId = serviceId,
                    Price = sp.Price
                });
            }

            // PHASE 3: Capacity AI
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
                catch (DbUpdateException ex)
                {
                    _context.Entry(dailyCapacity).State = EntityState.Detached;
                    dailyCapacity = await _context.DailySlotCapacities.FirstAsync(dc => dc.SlotId == slot.SlotId && dc.BranchId == request.BranchId && dc.Date == targetDateTime.Date);
                }
            }
            else
            {
               // Re-fetch to ensure we have the latest version before modifying
               dailyCapacity = await _context.DailySlotCapacities.FirstAsync(dc => dc.SlotId == slot.SlotId && dc.Date == targetDateTime.Date);
            }

            if (dailyCapacity.BookedWeight + maxCapacityWeight > slot.MaxCapacity)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Xưởng không đủ sức chứa cho xe này. Vui lòng chọn khung giờ khác.");

            // PHASE 4: Financial Math
            var (voucherDiscount, pointDiscount, pointsUsed, finalAmount, userVoucher) =
                await CalculateBookingPricingAsync(userId, totalOriginalPrice, request.VoucherId, request.PointsToUse, targetDateTime, vehicleTypeQuery.VehicleTypeId);

            // PHASE 5: Transaction
            var paymentMethod = request.PaymentMethod?.Trim() ?? "Wallet";
            var isPayOsPayment = string.Equals(paymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentMethod, "QR", StringComparison.OrdinalIgnoreCase);
            var isWalletPayment = string.Equals(paymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase);
            if (!isPayOsPayment && !isWalletPayment)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Phương thức thanh toán không hợp lệ. Chỉ hỗ trợ Wallet hoặc QR.");

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                wallet = new Wallet { UserId = userId, Balance = 0, Status = "Active" };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            if (!isPayOsPayment && wallet.Balance < finalAmount)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Số dư ví không đủ để đặt cọc. Cần: {finalAmount:N0}đ");

            try
            {
                // Update Capacity
                dailyCapacity.BookedWeight += maxCapacityWeight;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Có người khác vừa đặt lịch. Vui lòng thử lại.");
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
                    Description = $"Thanh toán cọc lịch rửa xe lúc {targetDateTime:dd/MM/yyyy HH:mm}"
                    };
                    _context.Transactions.Add(paymentTx);
                }

                // Apply Voucher & Points
                if (userVoucher != null)
                {
                    userVoucher.UsageCount += 1;
                    userVoucher.IsUsed = userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser;
                    userVoucher.UsedDate = DateTime.UtcNow;
                    userVoucher.LastUsedDate = DateTime.UtcNow;
                    userVoucher.Voucher.CurrentUsageCount += 1;
                }

                if (pointsUsed > 0)
                {
                    await _walletService.DeductSpendablePointsAsync(userId, pointsUsed, "Dùng điểm giảm giá đặt lịch");
                }

                // Create Booking
                var booking = new Booking
                {
                    UserId = userId,
                    VehicleId = vehicleTypeQuery.VehicleId,
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
                    FallbackQrCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper() // Generate a random QR string
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                if (paymentTx != null)
                {
                    paymentTx.ReferenceBookingId = booking.BookingId;
                }
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // PHASE 6: Post-Processing
                if (!isPayOsPayment)
                {
                    var user = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null && !string.IsNullOrEmpty(user.Email))
                    {
                        var emailHtml = EmailTemplateBuilder.BuildBookingConfirmationEmail(booking, pendingDetails, user.CustomerProfile?.FullName ?? "Quý khách");

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendEmailAsync(user.Email, $"[SmartWash] Đặt lịch thành công - #{booking.BookingId}", emailHtml);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Lỗi Background Email]: {ex.Message}");
                            }
                        });
                    }
                }

                var serviceNames = await _context.Services.Where(s => request.ServiceIds.Contains(s.ServiceId)).Select(s => s.ServiceName).ToListAsync();

                return new BookingResponseDTO
                {
                    BookingId = booking.BookingId,
                    LicensePlate = booking.LicensePlate,
                    ServiceNames = serviceNames,
                    ScheduledTime = booking.ScheduledTime,
                    Status = booking.Status,
                    OriginalPrice = booking.OriginalPrice,
                    PointDiscountAmount = booking.PointDiscountAmount,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    FinalAmount = booking.FinalAmount
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

        public async Task<List<BookingResponseDTO>> GetMyBookingsAsync(int userId)
        {
            return await _context.Bookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.ScheduledTime)
                .Select(b => new BookingResponseDTO
                {
                    BookingId = b.BookingId,
                    LicensePlate = b.LicensePlate ?? "",
                    ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName ?? "").ToList(),
                    ScheduledTime = b.ScheduledTime,
                    Status = b.Status ?? "",
                    OriginalPrice = b.OriginalPrice,
                    PointDiscountAmount = b.PointDiscountAmount,
                    VoucherDiscountAmount = b.VoucherDiscountAmount,
                    FinalAmount = b.FinalAmount
                })
                .ToListAsync();
        }

        public async Task<bool> CancelBookingAsync(int userId, int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingDetails)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy lịch hẹn.");
            if (booking.Status != "Pending") throw new AutoWashPro.BLL.Exceptions.BadRequestException("Chỉ có thể hủy lịch ở trạng thái đang chờ (Pending).");

            bool isRefundable = (booking.ScheduledTime - DateTime.UtcNow).TotalHours >= 4;

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
                            Description = $"Hoàn tiền cọc do hủy lịch #{booking.BookingId}",
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
                    // Late cancellation penalty
                    var userProfile = await _context.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
                }

                booking.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Có lỗi xảy ra khi hủy lịch (xung đột dữ liệu). Vui lòng thử lại.");
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
            CalculateBookingPricingAsync(int userId, decimal originalPrice, int? voucherId, int pointsToUseRequest, DateTime scheduledTime, int vehicleTypeId)
        {
            decimal voucherDiscount = 0;
            UserVoucher? userVoucher = null;

            if (voucherId.HasValue)
            {
                userVoucher = await _context.UserVouchers
                    .Include(uv => uv.Voucher)
                    .FirstOrDefaultAsync(uv => uv.VoucherId == voucherId.Value && uv.UserId == userId);

                if (userVoucher == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Bạn không sở hữu Voucher này.");
                if (userVoucher.Voucher.VehicleTypeId.HasValue && userVoucher.Voucher.VehicleTypeId.Value != vehicleTypeId)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher này không áp dụng cho loại xe của bạn.");
                }
                if (!userVoucher.Voucher.IsActive) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher này chưa được kích hoạt.");
                if (userVoucher.Voucher.StartDate.HasValue && userVoucher.Voucher.StartDate.Value > DateTime.UtcNow) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher này chưa đến thời gian áp dụng.");
                if (userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher này đã hết lượt sử dụng của bạn.");
                if (userVoucher.Voucher.MaxUsages > 0 && userVoucher.Voucher.CurrentUsageCount >= userVoucher.Voucher.MaxUsages) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher này đã hết tổng lượt sử dụng.");
                if (userVoucher.ExpiryDate < DateTime.UtcNow) throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher này đã hết hạn.");
                if (userVoucher.Voucher.MinOrderAmount > 0 && originalPrice < userVoucher.Voucher.MinOrderAmount)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Voucher này chỉ áp dụng cho đơn từ {userVoucher.Voucher.MinOrderAmount:N0}đ.");

                if (userVoucher.Voucher.VoucherType == AutoWashPro.DAL.Enums.VoucherType.PhysicalGift)
                {
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Voucher quà tặng hiện vật không thể dùng để trừ tiền hóa đơn.");
                }

                if (userVoucher.Voucher.ValidStartTime.HasValue && userVoucher.Voucher.ValidEndTime.HasValue)
                {
                    var timeOfDay = scheduledTime.ToVnTime().TimeOfDay;
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
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Voucher Happy Hour này chỉ áp dụng trong khung giờ từ {startTime:hh\\:mm} đến {endTime:hh\\:mm}.");
                    }
                }

                voucherDiscount = Math.Min(userVoucher.Voucher.DiscountAmount, originalPrice);
            }

            decimal remainingAfterVoucher = originalPrice - voucherDiscount;
            int pointsUsed = 0;
            decimal pointDiscount = 0;

            if (pointsToUseRequest > 0)
            {
                var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
                if (profile == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy hồ sơ khách hàng.");

                int maxPointsByBalance = profile.TotalPoint;
                int maxPointsByMoney = (int)(remainingAfterVoucher / PointConstants.VndPerSpendPoint);
                int pointsToApply = Math.Min(pointsToUseRequest, Math.Min(maxPointsByBalance, maxPointsByMoney));

                if (pointsToApply < pointsToUseRequest && pointsToApply == 0)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Không đủ điểm hoặc số tiền còn lại không cho phép dùng điểm.");

                pointsUsed = pointsToApply;
                pointDiscount = pointsUsed * PointConstants.VndPerSpendPoint;
            }

            decimal finalAmount = remainingAfterVoucher - pointDiscount;
            if (finalAmount < 0) finalAmount = 0;

            return (voucherDiscount, pointDiscount, pointsUsed, finalAmount, userVoucher);
        }

        public async Task<bool> UpdateVehicleConditionAsync(int staffId, int bookingId, UpdateVehicleConditionDTO request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.BookingDetails)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId);

                if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy lịch hẹn.");
                if (booking.Status != "CheckedIn")
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Chỉ có thể cập nhật tình trạng xe khi xe đã Check-in tại trạm.");

                booking.VehicleCondition = request.Condition;

                decimal newSurcharge = 0;
                var basePrice = booking.BookingDetails.Sum(bd => bd.Price);

                if (request.Condition == VehicleCondition.Dirty)
                {
                    newSurcharge += basePrice * 0.2m; // 20% Upsell
                }
                else if (request.Condition == VehicleCondition.VeryDirty)
                {
                    newSurcharge += basePrice * 0.5m; // 50% Upsell
                }

                if (request.ActualVehicleTypeId.HasValue)
                {
                    booking.ActualVehicleTypeId = request.ActualVehicleTypeId.Value;
                    // In a real scenario, we might look up the price difference between booked VehicleType and ActualVehicleType.
                    // For now, we apply a flat mismatch surcharge (e.g., 30% of base price)
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
                                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Khách hàng không đủ số dư để thanh toán phụ phí. Cần thêm: {surchargeDiff:N0}đ");
                            }

                            wallet.Balance -= surchargeDiff;

                            var paymentTx = new Transaction
                            {
                                WalletId = wallet.WalletId,
                                Amount = -surchargeDiff,
                                TransactionType = "Payment",
                                Description = $"Thanh toán phụ phí do xe dơ cho lịch #{booking.BookingId}",
                                ReferenceBookingId = booking.BookingId
                            };
                            _context.Transactions.Add(paymentTx);
                        }
                        else if (surchargeDiff < 0)
                        {
                            // Refund for downgrading condition
                            decimal refundAmount = Math.Abs(surchargeDiff);
                            wallet.Balance += refundAmount;

                            var refundTx = new Transaction
                            {
                                WalletId = wallet.WalletId,
                                Amount = refundAmount,
                                TransactionType = "Refund",
                                Description = $"Hoàn tiền phụ phí do thay đổi tình trạng xe cho lịch #{booking.BookingId}",
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
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy Booking.");

            booking.Status = "NoShow";
            // GIỮ NGUYÊN TIỀN CỌC. TUYỆT ĐỐI KHÔNG GỌI HÀM HOÀN TIỀN (REFUND) Ở ĐÂY.

            await _context.SaveChangesAsync();
        }

        private async Task<decimal> GetPriceFromDb(int serviceId, int vehicleTypeId, VehicleCondition condition, int branchId)
        {
            var servicePrice = await _context.ServicePrices
                .FirstOrDefaultAsync(sp => sp.ServiceId == serviceId && sp.VehicleTypeId == vehicleTypeId && sp.BranchId == branchId);

            if (servicePrice == null) return 0;

            decimal price = servicePrice.Price;
            if (condition == VehicleCondition.VeryDirty)
                price *= 1.2m;

            return price;
        }

        public async Task ReportMismatchAsync(int bookingId, AutoWashPro.BLL.Enums.VehicleConditionEnum condition, int actualTypeId)
        {
            var booking = await _context.Bookings.Include(b => b.BookingDetails).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy Booking.");

            booking.VehicleCondition = (AutoWashPro.DAL.Entities.VehicleCondition)condition;
            booking.ActualVehicleTypeId = actualTypeId;

            decimal totalNewPrice = 0;
            decimal totalOldPrice = 0;
            foreach (var detail in booking.BookingDetails)
            {
                totalOldPrice += detail.Price;
                totalNewPrice += await GetPriceFromDb(detail.ServiceId, actualTypeId, (AutoWashPro.DAL.Entities.VehicleCondition)condition, booking.BranchId);
            }

            if (totalNewPrice > totalOldPrice)
            {
                booking.MismatchSurcharge = totalNewPrice - totalOldPrice;

                // Mock Push Notification
                Console.WriteLine($"[PUSH] Thông báo tới User: Phát sinh phụ phí {booking.MismatchSurcharge} VNĐ do sai lệch loại xe/độ bẩn.");
            }

            await _context.SaveChangesAsync();
        }

        public async Task<WalkInBookingResponseDTO> CreateWalkInBookingAsync(int staffId, CreateWalkInBookingDTO request)
        {
            if (request.ServiceIds == null || request.ServiceIds.Count == 0)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Vui lòng chọn ít nhất 1 dịch vụ.");

            int? customerUserId = request.UserId == 0 ? (int?)null : request.UserId;
            var targetDateTime = DateTime.UtcNow;

            var normalizedPlate = request.LicensePlate.Replace("-", "").Replace(".", "").Trim().ToUpper();

            // Anti-hoarding Rule
            bool hasActiveBooking = await _context.Bookings.AnyAsync(b => b.LicensePlate == normalizedPlate && (b.Status == "Pending" || b.Status == "CheckedIn"));
            if (hasActiveBooking)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Xe biển số {normalizedPlate} đang có lịch hẹn chưa hoàn thành.");

            var existingVehicle = await _context.Vehicles
                .Include(v => v.VehicleType)
                .Where(v => v.LicensePlate == normalizedPlate && !v.IsDeleted)
                .FirstOrDefaultAsync();

            int resolvedVehicleTypeId;
            int resolvedVehicleId;
            int resolvedBaseWeight;

            if (existingVehicle != null)
            {
                resolvedVehicleId = existingVehicle.Id;
                resolvedBaseWeight = existingVehicle.VehicleType.BaseWeight;
                resolvedVehicleTypeId = request.VehicleTypeId.HasValue && request.VehicleTypeId.Value > 0
                    ? request.VehicleTypeId.Value
                    : existingVehicle.VehicleTypeId;
            }
            else if (request.VehicleTypeId.HasValue && request.VehicleTypeId.Value > 0)
            {
                var requestedType = await _context.VehicleTypes
                    .FirstOrDefaultAsync(vt => vt.Id == request.VehicleTypeId.Value);
                if (requestedType == null)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Loại xe {request.VehicleTypeId.Value} không tồn tại.");

                var otherCarModel = await _context.CarModels.FirstOrDefaultAsync(cm => cm.Name == "Khác");
                if (otherCarModel == null)
                {
                    otherCarModel = new CarModel { Name = "Khác", Brand = "Khác" };
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
                var otherVehicleType = await _context.VehicleTypes.FirstOrDefaultAsync(vt => vt.Name == "Khác");
                if (otherVehicleType == null)
                {
                    otherVehicleType = new VehicleType { Name = "Khác", BaseWeight = 1 };
                    _context.VehicleTypes.Add(otherVehicleType);
                    await _context.SaveChangesAsync();
                }

                var otherCarModel = await _context.CarModels.FirstOrDefaultAsync(cm => cm.Name == "Khác");
                if (otherCarModel == null)
                {
                    otherCarModel = new CarModel { Name = "Khác", Brand = "Khác" };
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
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Dịch vụ {serviceId} không hỗ trợ cho loại xe này tại chi nhánh này.");

                var actualWeight = sp.CapacityWeight > 0 ? sp.CapacityWeight : vehicleTypeQuery.BaseWeight;
                if (actualWeight > maxCapacityWeight) maxCapacityWeight = actualWeight;

                totalOriginalPrice += sp.Price;

                pendingDetails.Add(new BookingDetail
                {
                    ServiceId = serviceId,
                    Price = sp.Price
                });
            }

            // Find current time slot to update capacity
            var timeOfDay = targetDateTime.TimeOfDay;
            var slot = await _context.TimeSlots
                .Where(s => s.BranchId == request.BranchId && s.StartTime <= timeOfDay && s.EndTime >= timeOfDay)
                .FirstOrDefaultAsync();

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

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

                if (dailyCapacity.BookedWeight + maxCapacityWeight > slot.MaxCapacity)
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Xưởng không đủ sức chứa cho xe này ngay lúc này. Vui lòng thử lại sau.");

                dailyCapacity.BookedWeight += maxCapacityWeight;
            }

            string? paymentUrl = null;
            try
            {
                if (slot != null)
                {
                    try { await _context.SaveChangesAsync(); }
                    catch (DbUpdateConcurrencyException) { throw new AutoWashPro.BLL.Exceptions.BadRequestException("Có người khác vừa đặt lịch. Vui lòng thử lại."); }
                }

                var paymentMethod = request.PaymentMethod?.Trim() ?? "Cash";

                Booking booking;
                Transaction paymentTx;

                if (customerUserId == null) // THE GUEST FLOW
                {
                    if (string.Equals(paymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException("Khách vãng lai không thể thanh toán bằng Wallet.");
                    }

                    decimal finalAmount = totalOriginalPrice;

                    booking = new Booking
                    {
                        UserId = null,
                        VehicleId = vehicleTypeQuery.VehicleId,
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
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Transactions.Add(paymentTx);
                    await _context.SaveChangesAsync();

                    if (isPayOsWalkInPayment)
                    {
                        if (finalAmount <= 0)
                            throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Không thể tạo link thanh toán PayOS vì tổng tiền dịch vụ = {finalAmount:N0}đ. Vui lòng kiểm tra lại bảng giá dịch vụ cho loại xe này tại chi nhánh.");

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
                else // THE REGISTERED CUSTOMER FLOW
                {
                    var (voucherDiscount, pointDiscount, pointsUsed, finalAmount, userVoucher) =
                        await CalculateBookingPricingAsync(customerUserId.Value, totalOriginalPrice, request.VoucherId, request.PointsToUse, targetDateTime, vehicleTypeQuery.VehicleTypeId);

                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == customerUserId);
                    if (wallet == null || wallet.Balance < finalAmount)
                        throw new AutoWashPro.BLL.Exceptions.BadRequestException($"Số dư ví của khách hàng không đủ để thanh toán. Cần: {finalAmount:N0}đ");

                    wallet.Balance -= finalAmount;

                    paymentTx = new Transaction
                    {
                        WalletId = wallet.WalletId,
                        Amount = -finalAmount,
                        TransactionType = "Payment",
                        Description = $"Thanh toán khách vãng lai (có TK) lúc {targetDateTime:dd/MM/yyyy HH:mm}",
                        PaymentMethod = paymentMethod
                    };
                    _context.Transactions.Add(paymentTx);

                    if (userVoucher != null)
                    {
                        userVoucher.UsageCount += 1;
                        userVoucher.IsUsed = userVoucher.UsageCount >= userVoucher.Voucher.MaxUsagePerUser;
                        userVoucher.UsedDate = DateTime.UtcNow;
                        userVoucher.LastUsedDate = DateTime.UtcNow;
                        userVoucher.Voucher.CurrentUsageCount += 1;
                    }
                    if (pointsUsed > 0)
                    {
                        await _walletService.DeductSpendablePointsAsync(customerUserId.Value, pointsUsed, "Dùng điểm giảm giá đặt lịch vãng lai");
                    }

                    booking = new Booking
                    {
                        UserId = customerUserId,
                        VehicleId = vehicleTypeQuery.VehicleId,
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

                    paymentTx.ReferenceBookingId = booking.BookingId;
                    await _context.SaveChangesAsync();
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
                    PointDiscountAmount = booking.PointDiscountAmount,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    FinalAmount = booking.FinalAmount,
                    PaymentUrl = paymentUrl
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
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Vui lòng chọn ngày hoặc khung giờ để hủy lịch.");

            var query = _context.Bookings
                .Include(b => b.User)
                .Where(b => b.Status == "Pending" && b.BranchId == request.BranchId);

            if (request.AffectedDate.HasValue)
            {
                var targetDate = request.AffectedDate.Value.Date;
                query = query.Where(b => b.ScheduledTime.Date == targetDate);
            }

            if (request.TimeSlotId.HasValue)
            {
                var slot = await _context.TimeSlots.FindAsync(request.TimeSlotId.Value);
                if (slot != null)
                {
                    query = query.Where(b => b.ScheduledTime.TimeOfDay >= slot.StartTime && b.ScheduledTime.TimeOfDay <= slot.EndTime);
                }
            }

            var bookings = await query.ToListAsync();
            if (!bookings.Any()) return;

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                foreach (var booking in bookings)
                {
                    booking.Status = "CancelledBySystem";

                    var pendingPaymentTransactions = await _context.Transactions
                        .Where(t => t.ReferenceBookingId == booking.BookingId
                                 && t.TransactionType == "BookingPayment"
                                 && t.Status == "Pending")
                        .ToListAsync();
                    foreach (var pendingPaymentTransaction in pendingPaymentTransactions)
                    {
                        pendingPaymentTransaction.Status = "Expired";
                    }

                    if (booking.UserId.HasValue)
                    {
                        var userId = booking.UserId.Value;

                        if (booking.FinalAmount > 0 && await HasCompletedBookingPaymentAsync(booking.BookingId)) { await _walletService.RefundBalanceAsync(userId, booking.FinalAmount, $"Hoàn tiền hủy lịch tự động: {request.Reason}"); }

                        if (booking.PointsUsed > 0)
                        {
                            await _walletService.RefundSpendablePointsAsync(userId, booking.PointsUsed, $"Hoàn điểm hủy lịch tự động: {request.Reason}", booking.BookingId);
                        }

                        await _voucherService.GenerateCompensationVoucherAsync(userId);

                        if (booking.User != null && !string.IsNullOrEmpty(booking.User.Email))
                        {
                            _ = Task.Run(() => _emailService.SendEmailAsync(
                                booking.User.Email,
                                "AutoWashPro - Thông báo hủy lịch do sự cố",
                                $"Kính chào quý khách,<br/><br/>Chúng tôi rất tiếc phải thông báo lịch hẹn của quý khách vào lúc {booking.ScheduledTime:dd/MM/yyyy HH:mm} đã bị hủy do sự cố bất khả kháng.<br/>Lý do: {request.Reason}<br/><br/>Chúng tôi đã hoàn lại toàn bộ số tiền <b>{booking.FinalAmount:N0}đ</b> và điểm tích lũy (nếu có) vào ví của quý khách.<br/>Đồng thời, để tạ lỗi, chúng tôi đã gửi tặng quý khách 1 Voucher giảm giá 30,000đ (có hạn 7 ngày) vào tài khoản.<br/><br/>Trân trọng,<br/>AutoWashPro"
                            ));
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
                && (t.TransactionType == "Payment" || t.TransactionType == "BookingPayment" || t.TransactionType == "WalkInPayment"));
        }

        public async Task<BookingResponseDTO> RescheduleBookingAsync(int userId, int bookingId, RescheduleBookingDTO request)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Service)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null)
            {
                throw new AutoWashPro.BLL.Exceptions.NotFoundException("Không tìm thấy lịch hẹn.");
            }

            if (booking.Status != "Pending" && booking.Status != "Confirmed")
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Chỉ có thể thay đổi lịch hẹn ở trạng thái Pending hoặc Confirmed.");
            }

            var timeRemaining = booking.ScheduledTime - DateTime.UtcNow;
            if (timeRemaining.TotalHours < 2)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Chỉ có thể thay đổi lịch hẹn trước 2 tiếng so với giờ bắt đầu.");
            }

            var newSlot = await _context.TimeSlots.FindAsync(request.NewSlotId);
            if (newSlot == null || newSlot.BranchId != booking.BranchId)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Khung giờ mới không hợp lệ hoặc không thuộc cùng một chi nhánh.");
            }

            var newTargetDateTime = request.NewScheduledDate.Date.Add(newSlot.StartTime);

            if (newTargetDateTime <= DateTime.UtcNow)
            {
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("Không thể đặt lịch trong quá khứ.");
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
                    throw new AutoWashPro.BLL.Exceptions.BadRequestException("Khung giờ mới không đủ sức chứa cho xe của bạn.");
                }

                newCapacity.BookedWeight += booking.CapacityWeight;
                booking.ScheduledTime = newTargetDateTime;
                booking.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (booking.User != null && !string.IsNullOrEmpty(booking.User.Email))
                {
                    var emailHtml = $"Kính chào quý khách,<br/><br/>Lịch hẹn của quý khách đã được thay đổi thành công.<br/>Giờ hẹn mới: {newTargetDateTime:dd/MM/yyyy HH:mm}.<br/><br/>Trân trọng,<br/>AutoWashPro";
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(booking.User.Email, $"[SmartWash] Xác nhận thay đổi lịch hẹn - #{booking.BookingId}", emailHtml);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Lỗi Background Task - Email Reschedule] Booking #{booking.BookingId}: {ex.Message}");
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
                    PointDiscountAmount = booking.PointDiscountAmount,
                    VoucherDiscountAmount = booking.VoucherDiscountAmount,
                    FinalAmount = booking.FinalAmount
                };
            }
            catch (Exception)
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
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("So tien thanh toan PayOS phai lon hon 0.");

            if (amount != decimal.Truncate(amount))
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("PayOS chi ho tro so tien VND nguyen, khong co phan thap phan.");

            if (amount > int.MaxValue)
                throw new AutoWashPro.BLL.Exceptions.BadRequestException("So tien thanh toan PayOS vuot qua gioi han ho tro.");

            return decimal.ToInt32(amount);
        }

    }
}
