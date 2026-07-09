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
            // 1. Check phone
            var phoneExists = await _context.Users
                .AnyAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (phoneExists) throw new BadRequestException("This phone number is already registered.");

            // 2. Check email
            var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (emailExists) throw new BadRequestException("This email has already been used for registration.");

            // 3. Upload documents
            var businessLicenseUrl = await _cloudinaryService
                .UploadFileAsync(request.BusinessLicense, "business-documents");

            string? authorizationLetterUrl = null;
            if (request.AuthorizationLetter != null)
            {
                authorizationLetterUrl = await _cloudinaryService
                    .UploadFileAsync(request.AuthorizationLetter, "business-documents");
            }

            // 4. Create User + BusinessProfile
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
                    MonthlyCreditLimit = 0,
                    CurrentMonthUsage = 0,
                    DiscountPercent = 0,
                    ContractStartDate = DateTime.UtcNow,
                    ContractEndDate = DateTime.UtcNow.AddYears(1),
                    IsContractActive = false, // activated after approval
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
            return await _context.BusinessProfiles
                .Where(x => x.UserId == userId)
                .Select(x => new BusinessProfileResponseDTO
                {
                    BusinessProfileId = x.BusinessProfileId,
                    CompanyName = x.CompanyName,
                    TaxCode = x.TaxCode,
                    BusinessAddress = x.BusinessAddress,
                    ApprovalStatus = x.ApprovalStatus,
                    BusinessLicenseFileUrl = x.BusinessLicenseFileUrl,
                    AuthorizationLetterFileUrl = x.AuthorizationLetterFileUrl
                })
                .FirstOrDefaultAsync();
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

        public async Task<int> GenerateMonthlyInvoiceAsync(int businessProfileId, int year, int month)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x =>
                    x.BusinessProfileId == businessProfileId);

            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }

            var invoiceCode = $"MONTHLY-{businessProfileId}-{year}{month:00}";

            var existingInvoice = await _context.Invoices
                .FirstOrDefaultAsync(x =>
                    x.InvoiceCode == invoiceCode);

            if (existingInvoice != null)
            {
                throw new BadRequestException("Monthly invoice has already been created.");
            }

            var startDate = new DateTime(year, month, 1);

            var endDate = startDate.AddMonths(1);

            var completedWashes = await _context.FleetWashLogs
                .Include(x => x.Booking)
                .ThenInclude(x => x.BookingDetails)
                .ThenInclude(x => x.Service)
                .Where(x =>
                    x.Status == "Completed" &&
                    x.Booking != null &&
                    x.Booking.BusinessProfileId == businessProfileId &&
                    x.CompletedTime >= startDate &&
                    x.CompletedTime < endDate)
                    .ToListAsync();

            if (!completedWashes.Any())
            {
                throw new BadRequestException("No completed washes found in this time period.");
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
            invoice.TaxAmount = 0;
            invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;

            await _context.SaveChangesAsync();
            return invoice.InvoiceId;
        }
    }
}