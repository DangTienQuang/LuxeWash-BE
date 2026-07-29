using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.Services;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class BusinessServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<ICloudinaryService> _cloudinaryMock;
        private readonly BusinessService _sut;

        public BusinessServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _cloudinaryMock = new Mock<ICloudinaryService>();
            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/doc.pdf");
            _sut = new BusinessService(_dbContext, _cloudinaryMock.Object);
        }

        [Fact]
        public async Task RegisterBusinessUserAsync_DuplicatePhone_ThrowsBadRequestException()
        {
            _dbContext.Users.Add(new User { PhoneNumber = "0999999001", Email = "a@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = new RegisterBusinessUserRequest { PhoneNumber = "0999999001", Email = "new@test.com", Password = "pw123456", CompanyName = "Co" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RegisterBusinessUserAsync(request));
        }

        [Fact]
        public async Task RegisterBusinessUserAsync_DuplicateEmail_ThrowsBadRequestException()
        {
            _dbContext.Users.Add(new User { PhoneNumber = "0999999002", Email = "dupe@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" });
            await _dbContext.SaveChangesAsync();

            var request = new RegisterBusinessUserRequest { PhoneNumber = "0999999003", Email = "dupe@test.com", Password = "pw123456", CompanyName = "Co" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.RegisterBusinessUserAsync(request));
        }

        [Fact]
        public async Task RegisterBusinessUserAsync_NoAuthLetter_UploadsOnlyLicense()
        {
            var request = new RegisterBusinessUserRequest { PhoneNumber = "0999999004", Email = "biz1@test.com", Password = "pw123456", CompanyName = "Co" };

            var result = await _sut.RegisterBusinessUserAsync(request);

            Assert.Equal("Pending", result.ApprovalStatus);
            Assert.Null(result.AuthorizationLetterFileUrl);
            _cloudinaryMock.Verify(c => c.UploadFileAsync(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), "business-documents"), Times.Once);
        }

        [Fact]
        public async Task GetByUserIdAsync_NotFound_ReturnsNull()
        {
            var result = await _sut.GetByUserIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByUserIdAsync_Found_ReturnsDTO()
        {
            var user = new User { PhoneNumber = "0999999005", Email = "biz2@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            _dbContext.BusinessProfiles.Add(new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Fleet Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetByUserIdAsync(user.UserId);

            Assert.NotNull(result);
            Assert.Equal("Fleet Co", result.CompanyName);
        }

        [Fact]
        public async Task ReviewBusinessProfileAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new ReviewBusinessProfileDTO { BusinessProfileId = 999, IsApproved = true };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ReviewBusinessProfileAsync(1, dto));
        }

        [Fact]
        public async Task ReviewBusinessProfileAsync_AlreadyReviewed_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999999006", Email = "biz3@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewBusinessProfileDTO { BusinessProfileId = profile.BusinessProfileId, IsApproved = true };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ReviewBusinessProfileAsync(1, dto));
        }

        [Fact]
        public async Task ReviewBusinessProfileAsync_Approve_SetsApprovedAndUpdatesRole()
        {
            var user = new User { PhoneNumber = "0999999007", Email = "biz4@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Co",
                ApprovalStatus = "Pending",
                IsContractActive = false,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewBusinessProfileDTO { BusinessProfileId = profile.BusinessProfileId, IsApproved = true };
            await _sut.ReviewBusinessProfileAsync(99, dto);

            var updatedProfile = await _dbContext.BusinessProfiles.FirstAsync(p => p.BusinessProfileId == profile.BusinessProfileId);
            var updatedUser = await _dbContext.Users.FirstAsync(u => u.UserId == user.UserId);
            Assert.Equal("Approved", updatedProfile.ApprovalStatus);
            Assert.Equal("Business", updatedUser.Role);
        }

        [Fact]
        public async Task ReviewBusinessProfileAsync_Reject_SetsRejectedWithReason()
        {
            var user = new User { PhoneNumber = "0999999008", Email = "biz5@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Co",
                ApprovalStatus = "Pending",
                IsContractActive = false,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewBusinessProfileDTO { BusinessProfileId = profile.BusinessProfileId, IsApproved = false, RejectionReason = "Invalid documents" };
            await _sut.ReviewBusinessProfileAsync(99, dto);

            var updatedProfile = await _dbContext.BusinessProfiles.FirstAsync(p => p.BusinessProfileId == profile.BusinessProfileId);
            Assert.Equal("Rejected", updatedProfile.ApprovalStatus);
            Assert.Equal("Invalid documents", updatedProfile.RejectionReason);
        }

        [Fact]
        public async Task GetPendingBusinessApplicationsAsync_ReturnsOnlyPending()
        {
            var user1 = new User { PhoneNumber = "0999999009", Email = "biz6@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            var user2 = new User { PhoneNumber = "0999999010", Email = "biz7@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.AddRange(user1, user2);
            await _dbContext.SaveChangesAsync();

            _dbContext.BusinessProfiles.AddRange(
                new BusinessProfile { UserId = user1.UserId, CompanyName = "Pending Co", ApprovalStatus = "Pending", IsContractActive = false, BusinessLicenseFileUrl = "x", CreatedAt = DateTime.UtcNow, ContractStartDate = DateTime.UtcNow, ContractEndDate = DateTime.UtcNow.AddYears(1) },
                new BusinessProfile { UserId = user2.UserId, CompanyName = "Approved Co", ApprovalStatus = "Approved", IsContractActive = true, BusinessLicenseFileUrl = "x", CreatedAt = DateTime.UtcNow, ContractStartDate = DateTime.UtcNow, ContractEndDate = DateTime.UtcNow.AddYears(1) }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetPendingBusinessApplicationsAsync();

            Assert.Single(result);
            Assert.Equal("Pending Co", result[0].CompanyName);
        }

        [Fact]
        public async Task GetBusinessApplicationDetailAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetBusinessApplicationDetailAsync(999));
        }

        [Fact]
        public async Task GetBusinessApplicationDetailAsync_Found_ReturnsDTO()
        {
            var user = new User { PhoneNumber = "0999999011", Email = "biz8@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Detail Co",
                ApprovalStatus = "Pending",
                IsContractActive = false,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBusinessApplicationDetailAsync(profile.BusinessProfileId);

            Assert.Equal("Detail Co", result.CompanyName);
        }

        [Fact]
        public async Task GetInvoiceExportAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetInvoiceExportAsync(999));
        }

        [Fact]
        public async Task GetInvoiceExportAsync_Found_MapsFieldsWithFallbacks()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            var booking = new Booking { LicensePlate = "51A11111", Status = "Completed", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var invoice = new Invoice
            {
                InvoiceCode = "MONTHLY-1-202607",
                BookingId = booking.BookingId,
                Status = "Issued",
                InvoiceType = "MonthlyStatement",
                Subtotal = 100000,
                TaxAmount = 0,
                TotalAmount = 100000,
                IssuedAt = DateTime.UtcNow
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetInvoiceExportAsync(invoice.InvoiceId);

            Assert.Equal("51A11111", result.LicensePlate); // falls back to booking.LicensePlate since no FleetVehicle
            Assert.Equal("Branch A", result.BranchName);
            Assert.Equal("202607", result.BillingPeriod);
        }

        [Fact]
        public async Task GenerateMonthlyInvoiceAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GenerateMonthlyInvoiceAsync(999, 2026, 7));
        }

        [Fact]
        public async Task GenerateMonthlyInvoiceAsync_AlreadyExists_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999999012", Email = "biz9@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            _dbContext.Invoices.Add(new Invoice { InvoiceCode = $"MONTHLY-{profile.BusinessProfileId}-202607", BusinessProfileId = profile.BusinessProfileId, Status = "Issued", InvoiceType = "MonthlyStatement", IssuedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GenerateMonthlyInvoiceAsync(profile.BusinessProfileId, 2026, 7));
        }

        [Fact]
        public async Task GenerateMonthlyInvoiceAsync_NoCompletedWashes_ThrowsBadRequestException()
        {
            var user = new User { PhoneNumber = "0999999013", Email = "biz10@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GenerateMonthlyInvoiceAsync(profile.BusinessProfileId, 2026, 7));
        }

        [Fact]
        public async Task GenerateMonthlyInvoiceAsync_Valid_CreatesInvoiceWithItemsAndTotals()
        {
            var user = new User { PhoneNumber = "0999999014", Email = "biz11@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            var profile = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(profile);

            var service = new Service { ServiceName = "Fleet Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var washDate = new DateTime(2026, 7, 15);
            var booking = new Booking
            {
                BusinessProfileId = profile.BusinessProfileId,
                LicensePlate = "51B22222",
                Status = "Completed",
                BranchId = 1,
                ScheduledTime = washDate,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = 1, BranchId = 1, BookingId = booking.BookingId, CheckInTime = washDate, CompletedTime = washDate, Status = "Completed", WashCost = 100000 });
            await _dbContext.SaveChangesAsync();

            var invoiceId = await _sut.GenerateMonthlyInvoiceAsync(profile.BusinessProfileId, 2026, 7);

            var invoice = await _dbContext.Invoices.FirstAsync(i => i.InvoiceId == invoiceId);
            Assert.Equal(100000, invoice.Subtotal);
            Assert.Equal(100000, invoice.TotalAmount);

            var items = await _dbContext.InvoiceItems.Where(i => i.InvoiceId == invoiceId).ToListAsync();
            Assert.Single(items);
        }
    }
}