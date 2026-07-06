using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Helpers;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class DataSeedingService : IDataSeedingService
    {
        private readonly AutoWashDbContext _context;
        private readonly ILogger<DataSeedingService> _logger;

        public DataSeedingService(AutoWashDbContext context, ILogger<DataSeedingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Booking> SeedTestBookingForAIAsync(string licensePlate = "30A-888.88")
        {
            _logger.LogInformation("Bắt đầu khởi tạo dữ liệu test AI cho biển số: {Plate}", licensePlate);

            // 1. Kiểm tra / Tạo Tier (Hạng thành viên)
            var tier = await _context.Tiers.FirstOrDefaultAsync();
            if (tier == null)
            {
                tier = new Tier
                {
                    TierName = "Gold Test AI",
                    PointMultiplier = 1.2,
                    BookingWindowDays = 30,
                    MinAccumulatedPoints = 0
                };
                _context.Tiers.Add(tier);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo Tier mới: {TierId}", tier.TierId);
            }

            // 2. Kiểm tra / Tạo User & CustomerProfile
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .FirstOrDefaultAsync(u => u.PhoneNumber == "0988888888" || u.Email == "testai@smartwash.vn");

            if (user == null)
            {
                user = new User
                {
                    PhoneNumber = "0988888888",
                    Email = "testai@smartwash.vn",
                    PasswordHash = "AQAAAAEAACcQAAAAE...", // Dummy hash
                    Role = "Customer",
                    Status = "Active"
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var profile = new CustomerProfile
                {
                    UserId = user.UserId,
                    FullName = "Khách Hàng Test AI Camera",
                    TierId = tier.TierId,
                    TotalPoint = 1000,
                    PromotionPoint = 500,
                    CurrentYearTierPoints = 1000
                };
                _context.CustomerProfiles.Add(profile);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo User test AI: {UserId}", user.UserId);
            }
            else if (user.CustomerProfile == null)
            {
                var profile = new CustomerProfile
                {
                    UserId = user.UserId,
                    FullName = "Khách Hàng Test AI Camera",
                    TierId = tier.TierId,
                    TotalPoint = 1000
                };
                _context.CustomerProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            // 3. Kiểm tra / Tạo VehicleType
            var vehicleType = await _context.VehicleTypes.FirstOrDefaultAsync();
            if (vehicleType == null)
            {
                vehicleType = new VehicleType
                {
                    Name = "Sedan 4-5 chỗ",
                    Description = "Xe con 4-5 chỗ thông thường",
                    BaseWeight = 1
                };
                _context.VehicleTypes.Add(vehicleType);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo VehicleType mới: {TypeId}", vehicleType.Id);
            }

            // 4. Kiểm tra / Tạo Vehicle
            var normalizedInputPlate = new string(licensePlate.ToUpper().Where(char.IsLetterOrDigit).ToArray());
            var vehicles = await _context.Vehicles.ToListAsync();
            var vehicle = vehicles.FirstOrDefault(v =>
                new string(v.LicensePlate.ToUpper().Where(char.IsLetterOrDigit).ToArray()) == normalizedInputPlate);

            if (vehicle == null)
            {
                vehicle = new Vehicle
                {
                    LicensePlate = licensePlate,
                    UserId = user.UserId,
                    VehicleTypeId = vehicleType.Id,
                    CarModel = "Mercedes-Benz S500 (AI Test)",
                    IsDeleted = false
                };
                _context.Vehicles.Add(vehicle);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo Vehicle mới: {VehicleId} - {Plate}", vehicle.Id, licensePlate);
            }
            else
            {
                if (vehicle.IsDeleted)
                {
                    vehicle.IsDeleted = false;
                }
                if (vehicle.UserId == null || vehicle.UserId == 0)
                {
                    vehicle.UserId = user.UserId;
                }
                await _context.SaveChangesAsync();
            }

            // 5. Kiểm tra / Tạo Branch
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive);
            if (branch == null)
            {
                branch = new Branch
                {
                    Name = "SmartWash Chi Nhánh Trung Tâm (Test AI)",
                    Address = "Số 1 Trần Duy Hưng, Cầu Giấy, Hà Nội",
                    IsActive = true,
                    AllowNegativeStock = true
                };
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo Branch mới: {BranchId}", branch.BranchId);
            }

            // 6. Kiểm tra / Tạo Service
            var service = await _context.Services.FirstOrDefaultAsync(s => s.IsActive);
            if (service == null)
            {
                service = new Service
                {
                    ServiceName = "Rửa xe công nghệ cao & Quét AI",
                    Description = "Dịch vụ rửa xe tự động nhận diện biển số bằng AI",
                    IsActive = true
                };
                _context.Services.Add(service);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo Service mới: {ServiceId}", service.ServiceId);
            }

            // 7. Kiểm tra / Tạo ServicePrice
            var servicePrice = await _context.ServicePrices.FirstOrDefaultAsync(sp =>
                sp.ServiceId == service.ServiceId &&
                sp.BranchId == branch.BranchId &&
                sp.VehicleTypeId == vehicleType.Id);

            if (servicePrice == null)
            {
                servicePrice = new ServicePrice
                {
                    ServiceId = service.ServiceId,
                    BranchId = branch.BranchId,
                    VehicleTypeId = vehicleType.Id,
                    Price = 100000m,
                    CapacityWeight = 1,
                    EstimatedDurationMinutes = 30
                };
                _context.ServicePrices.Add(servicePrice);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo ServicePrice mới: {PriceId}", servicePrice.ServicePriceId);
            }

            // 8. Xử lý Booking trong ngày hôm nay (VN time) cho biển số này
            var todayInVN = DateTime.UtcNow.ToVnTime().Date;

            // Lấy các booking trong khoảng +-24h để bao phủ ngày hôm nay VN
            var startTime = DateTime.UtcNow.AddHours(-24);
            var endTime = DateTime.UtcNow.AddHours(24);

            var candidateBookings = await _context.Bookings
                .Include(b => b.BookingDetails)
                .Where(b => b.ScheduledTime >= startTime && b.ScheduledTime <= endTime)
                .ToListAsync();

            var todaysBookings = candidateBookings.Where(b =>
                new string(b.LicensePlate.ToUpper().Where(char.IsLetterOrDigit).ToArray()) == normalizedInputPlate &&
                b.ScheduledTime.ToVnTime().Date == todayInVN
            ).ToList();

            Booking mainBooking;

            if (todaysBookings.Count > 0)
            {
                // Sử dụng booking đầu tiên làm booking test chính
                mainBooking = todaysBookings.First();
                mainBooking.Status = "Pending";
                mainBooking.ScheduledTime = DateTime.UtcNow;
                mainBooking.BranchId = branch.BranchId;
                mainBooking.UserId = user.UserId;
                mainBooking.VehicleId = vehicle.Id;
                mainBooking.LicensePlate = licensePlate;
                mainBooking.OriginalPrice = 100000m;
                mainBooking.FinalAmount = 100000m;
                mainBooking.ActualVehicleTypeId = vehicleType.Id;
                mainBooking.UpdatedAt = DateTime.UtcNow;

                // Nếu có nhiều hơn 1 booking trong hôm nay, dời các booking dư thừa về hôm qua
                // để tránh lỗi "Phát hiện nhiều lịch hẹn trong ngày hôm nay" khi quét camera AI
                for (int i = 1; i < todaysBookings.Count; i++)
                {
                    todaysBookings[i].ScheduledTime = DateTime.UtcNow.AddDays(-2);
                    todaysBookings[i].Status = "CancelledBySystem";
                    todaysBookings[i].UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã cập nhật/reset Booking cũ {BookingId} về trạng thái Pending cho hôm nay", mainBooking.BookingId);
            }
            else
            {
                // Tạo mới Booking
                mainBooking = new Booking
                {
                    UserId = user.UserId,
                    BranchId = branch.BranchId,
                    VehicleId = vehicle.Id,
                    LicensePlate = licensePlate,
                    ScheduledTime = DateTime.UtcNow, // Thuộc ngày hôm nay VN
                    Status = "Pending",
                    OriginalPrice = 100000m,
                    FinalAmount = 100000m,
                    CapacityWeight = 1,
                    VehicleCondition = AutoWashPro.DAL.Entities.VehicleCondition.Clean,
                    ActualVehicleTypeId = vehicleType.Id,
                    BookingType = "Personal",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Bookings.Add(mainBooking);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo mới Booking {BookingId} ở trạng thái Pending", mainBooking.BookingId);
            }

            // 9. Đảm bảo Booking có BookingDetail
            if (!mainBooking.BookingDetails.Any())
            {
                var bookingDetail = new BookingDetail
                {
                    BookingId = mainBooking.BookingId,
                    ServiceId = service.ServiceId,
                    Price = 100000m
                };
                _context.BookingDetails.Add(bookingDetail);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã thêm BookingDetail cho Booking {BookingId}", mainBooking.BookingId);
            }

            // 10. Đảm bảo Booking đã có Transaction thanh toán Completed
            // (Vì BookingService.UpdateBookingStatusAsync kiểm tra HasCompletedBookingPaymentAsync khi Check-in/Completed)
            var hasPayment = await _context.Transactions.AnyAsync(t =>
                t.ReferenceBookingId == mainBooking.BookingId &&
                t.Status == "Completed" &&
                (t.TransactionType == "Payment" || t.TransactionType == "BookingPayment" || t.TransactionType == "WalkInPayment"));

            if (!hasPayment)
            {
                var transaction = new Transaction
                {
                    Amount = mainBooking.FinalAmount,
                    TransactionType = "BookingPayment",
                    Description = $"Thanh toán test AI camera cho lịch hẹn {mainBooking.BookingId} - Biển số {licensePlate}",
                    ReferenceBookingId = mainBooking.BookingId,
                    Status = "Completed",
                    PaymentMethod = "AI_Test_Seed",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Đã tạo Transaction thanh toán cho Booking {BookingId}", mainBooking.BookingId);
            }

            return mainBooking;
        }
    }
}
