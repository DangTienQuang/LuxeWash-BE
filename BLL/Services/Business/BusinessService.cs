#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
namespace BLL.Services
{
    public class BusinessService : IBusinessService
    {
        private readonly AutoWashDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;
        public BusinessService(AutoWashDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }
        public async Task<RegisterBusinessUserResponse> RegisterBusinessUserAsync(RegisterBusinessUserRequest request)
        {
            var phoneExists = await _context.Users
                .AnyAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (phoneExists) throw new BadRequestException("This phone number is already registered.");
            var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (emailExists) throw new BadRequestException("This email has already been used for registration.");
            var businessLicenseUrl = await _cloudinaryService
                .UploadFileAsync(request.BusinessLicense, "business-documents");
            string? authorizationLetterUrl = null;
            if (request.AuthorizationLetter != null)
            {
                authorizationLetterUrl = await _cloudinaryService
                    .UploadFileAsync(request.AuthorizationLetter, "business-documents");
            }
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    PhoneNumber = request.PhoneNumber,
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = "Business",
                    Status = "Active",
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                var profile = new BusinessProfile
                {
                    UserId = user.UserId,
                    CompanyName = request.CompanyName,
                    TaxCode = request.TaxCode,
                    BusinessAddress = request.BusinessAddress,
                    BillingEmail = request.BillingEmail,
                    RepresentativeName = request.RepresentativeName,
                    PaymentTermDays = request.PaymentTermDays,
                    ApprovalStatus = "Pending",
                    BusinessLicenseFileUrl = businessLicenseUrl,
                    AuthorizationLetterFileUrl = authorizationLetterUrl,
                    CreatedAt = DateTime.UtcNow,
                    MonthlyCreditLimit = request.MonthlyCreditLimit,
                    CurrentMonthUsage = 0,
                    DiscountPercent = 0,
                    ContractStartDate = DateTime.UtcNow,
                    ContractEndDate = DateTime.UtcNow.AddYears(1),
                    IsContractActive = false, 
                };
                _context.BusinessProfiles.Add(profile);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return new RegisterBusinessUserResponse
                {
                    UserId = user.UserId,
                    PhoneNumber = user.PhoneNumber,
                    Role = user.Role,
                    BusinessProfileId = profile.BusinessProfileId,
                    CompanyName = profile.CompanyName,
                    ApprovalStatus = profile.ApprovalStatus,
                    BusinessLicenseFileUrl = profile.BusinessLicenseFileUrl,
                    AuthorizationLetterFileUrl = profile.AuthorizationLetterFileUrl,
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<BusinessProfileResponseDTO?> GetByUserIdAsync(int userId)
        {
            var profile = await _context.BusinessProfiles
                .Where(x => x.UserId == userId)
                .Select(x => new BusinessProfileResponseDTO
                {
                    BusinessProfileId = x.BusinessProfileId,
                    CompanyName = x.CompanyName,
                    TaxCode = x.TaxCode,
                    BusinessAddress = x.BusinessAddress,
                    BillingEmail = x.BillingEmail,
                    RepresentativeName = x.RepresentativeName,
                    PaymentTermDays = x.PaymentTermDays,
                    ApprovalStatus = x.ApprovalStatus,
                    BusinessLicenseFileUrl = x.BusinessLicenseFileUrl,
                    AuthorizationLetterFileUrl = x.AuthorizationLetterFileUrl,
                    MonthlyCreditLimit = x.MonthlyCreditLimit,
                    DiscountPercent = x.DiscountPercent,
                    ContractStartDate = x.ContractStartDate,
                    ContractEndDate = x.ContractEndDate,
                    IsContractActive = x.IsContractActive
                })
                .FirstOrDefaultAsync();

            if (profile == null) return null;

            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var startOfNextMonth = startOfMonth.AddMonths(1);
            profile.CurrentMonthUsage = await _context.FleetWashLogs
                .Where(log =>
                    log.FleetVehicle.BusinessProfileId == profile.BusinessProfileId &&
                    log.CheckInTime >= startOfMonth &&
                    log.CheckInTime < startOfNextMonth &&
                    log.Status != "Cancelled")
                .SumAsync(log => (decimal?)log.WashCost) ?? 0;

            return profile;
        }
        public async Task ReviewBusinessProfileAsync(int reviewerId, ReviewBusinessProfileDTO dto)
        {
            var profile = await _context.BusinessProfiles
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.BusinessProfileId == dto.BusinessProfileId);
            if (profile == null)
            {
                throw new NotFoundException("Business profile not found.");
            }
            if (profile.ApprovalStatus != "Pending")
            {
                throw new BadRequestException("Application has already been reviewed.");
            }
            profile.ReviewedByUserId = reviewerId;
            profile.ReviewedAt = DateTime.UtcNow;
            if (dto.IsApproved)
            {
                profile.ApprovalStatus = "Approved";
                profile.IsContractActive = true;
                profile.User.Role = "Business";
            }
            else
            {
                profile.ApprovalStatus = "Rejected";
                profile.RejectionReason = dto.RejectionReason;
            }
            await _context.SaveChangesAsync();
        }
        public async Task<List<PendingBusinessApplicationDTO>> GetPendingBusinessApplicationsAsync()
        {
            return await _context.BusinessProfiles
                .Where(x => x.ApprovalStatus == "Pending")
                .Select(x => new PendingBusinessApplicationDTO
                {
                    BusinessProfileId = x.BusinessProfileId,
                    CompanyName = x.CompanyName,
                    TaxCode = x.TaxCode,
                    BusinessAddress = x.BusinessAddress,
                    BillingEmail = x.BillingEmail,
                    RepresentativeName = x.RepresentativeName,
                    BusinessLicenseFileUrl = x.BusinessLicenseFileUrl,
                    AuthorizationLetterFileUrl = x.AuthorizationLetterFileUrl,
                    PaymentTermDays = x.PaymentTermDays,
                    MonthlyCreditLimit = x.MonthlyCreditLimit,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }
        public async Task<PendingBusinessApplicationDTO?> GetBusinessApplicationDetailAsync(int businessProfileId)
        {
            var profile = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.BusinessProfileId == businessProfileId);
            if (profile == null)
            {
                throw new NotFoundException("Business application not found.");
            }
            return new PendingBusinessApplicationDTO
            {
                BusinessProfileId = profile.BusinessProfileId,
                CompanyName = profile.CompanyName,
                TaxCode = profile.TaxCode,
                BusinessAddress = profile.BusinessAddress,
                BillingEmail = profile.BillingEmail,
                RepresentativeName = profile.RepresentativeName,
                ApprovalStatus = profile.ApprovalStatus,
                RejectionReason = profile.RejectionReason,
                BusinessLicenseFileUrl = profile.BusinessLicenseFileUrl,
                AuthorizationLetterFileUrl = profile.AuthorizationLetterFileUrl,
                PaymentTermDays = profile.PaymentTermDays,
                MonthlyCreditLimit = profile.MonthlyCreditLimit,
                CreatedAt = profile.CreatedAt
            };
        }
        public async Task<InvoiceExportDTO> GetInvoiceExportAsync(int invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.BusinessProfile)
                .Include(i => i.Booking)
                    .ThenInclude(b => b.Branch)
                .Include(i => i.Booking)
                    .ThenInclude(b => b.FleetVehicle)
                        .ThenInclude(f => f.VehicleType)
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            if (invoice == null)
            {
                throw new NotFoundException("Invoice not found.");
            }
            return new InvoiceExportDTO
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceCode = invoice.InvoiceCode,
                CreatedAt = invoice.IssuedAt,
                Subtotal = invoice.Subtotal,
                TaxAmount = invoice.TaxAmount,
                TotalAmount = invoice.TotalAmount,
                BusinessName = invoice.BusinessProfile?.CompanyName ?? "",
                Status = invoice.Status,
                InvoiceType = invoice.InvoiceType,
                BillingPeriod = invoice.InvoiceCode.Split('-').Last(),
                BusinessAddress = invoice.BusinessProfile?.BusinessAddress ?? "",
                BillingEmail = invoice.BusinessProfile?.BillingEmail ?? "",
                RepresentativeName = invoice.BusinessProfile?.RepresentativeName ?? "",
                TaxCode = invoice.BusinessProfile?.TaxCode ?? "",
                LicensePlate = invoice.Booking?.FleetVehicle?.LicensePlate ??
                    invoice.Booking?.LicensePlate ?? "",
                VehicleType = invoice.Booking?.FleetVehicle?.VehicleType?.Name ?? "",
                BranchName = invoice.Booking?.Branch?.Name ?? "",
                BranchAddress = invoice.Booking?.Branch?.Address ?? "",
                BookingId = invoice.BookingId ?? 0,
                Items = invoice.InvoiceItems
                    .Select(x => new InvoiceItemDTO
                    {
                        InvoiceItemId = x.InvoiceItemId,
                        Description = x.Description,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        Amount = x.Amount
                    })
                    .ToList()
            };
        }
        public async Task<List<BillingBusinessDTO>> GetBillingBusinessesAsync()
        {
            return await _context.BusinessProfiles
                .AsNoTracking()
                .Where(x => x.ApprovalStatus == "Approved" && x.IsContractActive)
                .OrderBy(x => x.CompanyName)
                .Select(x => new BillingBusinessDTO
                {
                    BusinessProfileId = x.BusinessProfileId,
                    CompanyName = x.CompanyName,
                    BillingEmail = x.BillingEmail ?? string.Empty,
                    TaxCode = x.TaxCode ?? string.Empty,
                    ApprovalStatus = x.ApprovalStatus
                })
                .ToListAsync();
        }
        public async Task<int> GenerateMonthlyInvoiceAsync(int businessProfileId, int year, int month)
        {
            if (month < 1 || month > 12)
                throw new BadRequestException("Billing month is invalid.");
            if (year < 2000 || year > 9999)
                throw new BadRequestException("Billing year is invalid.");

            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x =>
                    x.BusinessProfileId == businessProfileId);
            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }
            // Multiple statements can intentionally be issued for the same
            // business and month. Each new statement only includes completed
            // washes that have not appeared on an earlier monthly statement.
            var invoiceCode =
                $"MONTHLY-{businessProfileId}-{year}{month:00}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(100, 999)}";
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);
            var alreadyInvoicedBookingIds = _context.InvoiceItems
                .Where(x =>
                    x.Invoice.BusinessProfileId == businessProfileId &&
                    x.Invoice.InvoiceType == "MonthlyStatement")
                .Select(x => x.BookingDetail.BookingId);
            var completedWashes = await _context.FleetWashLogs
                .Include(x => x.Booking)
                .ThenInclude(x => x.BookingDetails)
                .ThenInclude(x => x.Service)
                .Where(x =>
                    x.Status == "Completed" &&
                    x.Booking != null &&
                    x.Booking.BusinessProfileId == businessProfileId &&
                    x.CompletedTime >= startDate &&
                    x.CompletedTime < endDate &&
                    x.BookingId.HasValue &&
                    !alreadyInvoicedBookingIds.Contains(x.BookingId.Value))
                    .ToListAsync();
            if (!completedWashes.Any())
            {
                throw new BadRequestException(
                    "Không có lượt rửa đã hoàn thành nào chưa được xuất hóa đơn trong kỳ đã chọn.",
                    "NO_UNINVOICED_COMPLETED_WASHES");
            }
            var invoice = new Invoice
            {
                InvoiceCode = invoiceCode,
                BusinessProfileId = businessProfileId,
                InvoiceType = "MonthlyStatement",
                Status = "Issued",
                IssuedAt = DateTime.UtcNow
            };
            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
            var invoiceItems = new List<InvoiceItem>();
            foreach (var wash in completedWashes)
            {
                var booking = wash.Booking;
                if (booking == null)
                    continue;
                foreach (var detail in booking.BookingDetails)
                {
                    invoiceItems.Add(new InvoiceItem
                    {
                        InvoiceId = invoice.InvoiceId,
                        BookingDetailId = detail.DetailId,
                        Description = $"{detail.Service.ServiceName} - {booking.LicensePlate}",
                        Quantity = 1,
                        UnitPrice = detail.Price,
                        Amount = detail.Price
                    });
                }
            }
            if (!invoiceItems.Any())
            {
                throw new BadRequestException("Could not create any items for invoice.");
            }
            await _context.InvoiceItems.AddRangeAsync(invoiceItems);
            invoice.Subtotal = invoiceItems.Sum(x => x.Amount);
            invoice.TaxAmount = Math.Round(invoice.Subtotal * 0.08m, 0, MidpointRounding.AwayFromZero);
            invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;
            await _context.SaveChangesAsync();
            return invoice.InvoiceId;
        }
    }
}
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
