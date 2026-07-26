using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using DAL.Entities;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.Services;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Http;

namespace AutoWashPro.Tests
{
    public class BusinessServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly Mock<ICloudinaryService> _mockCloudinaryService;
        private readonly BusinessService _businessService;

        public BusinessServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AutoWashDbContext(options);
            _mockCloudinaryService = new Mock<ICloudinaryService>(MockBehavior.Default);

            _businessService = new BusinessService(_context, _mockCloudinaryService.Object);
        }

        [Fact]
        public async Task RegisterBusinessUserAsync_ValidPayload_CreatesProfile_TC()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            
            _mockCloudinaryService.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
                .ReturnsAsync("http://fake.cloudinary/url.jpg");

            var request = new RegisterBusinessUserRequest
            {
                Email = "b2b@company.com",
                Password = "Password123!",
                CompanyName = "Test Company",
                TaxCode = "0101234567",
                RepresentativeName = "John Doe",
                PhoneNumber = "0901234567",
                BusinessLicense = mockFile.Object
            };

            // Act
            var result = await _businessService.RegisterBusinessUserAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("b2b@company.com", _context.Users.FirstOrDefault(u => u.UserId == result.UserId)?.Email);
            
            var userInDb = await _context.Users.FirstOrDefaultAsync(u => u.Email == "b2b@company.com");
            Assert.NotNull(userInDb);
            Assert.Equal("Business", userInDb.Role);

            var profileInDb = await _context.BusinessProfiles.FirstOrDefaultAsync(p => p.UserId == userInDb.UserId);
            Assert.NotNull(profileInDb);
            Assert.Equal("Test Company", profileInDb.CompanyName);
            Assert.Equal("Pending", profileInDb.ApprovalStatus);
        }

        [Fact]
        public async Task GenerateMonthlyInvoiceAsync_ValidBusiness_CalculatesCorrectly_TC()
        {
            // Arrange
            int userId = 10;
            int businessProfileId = 10;
            
            _context.Users.Add(new User { UserId = userId, Email = "b2b2@company.com", PasswordHash = "hash", Role = "Business", Status = "Active", PhoneNumber = "0987654321" });
            _context.BusinessProfiles.Add(new BusinessProfile { 
                BusinessProfileId = businessProfileId, 
                UserId = userId, 
                CompanyName = "Test Company 2", 
                TaxCode = "999999999", 
                ApprovalStatus = "Approved",
                BusinessLicenseFileUrl = "http://fake.url"
            });
            _context.Branches.Add(new Branch { BranchId = 1, Name = "Branch 1" });
            _context.VehicleTypes.Add(new VehicleType { Id = 1, Name = "SUV" });
            _context.FleetVehicles.Add(new FleetVehicle { FleetVehicleId = 1, BusinessProfileId = businessProfileId, VehicleTypeId = 1, LicensePlate = "51F-12345", Status = "Active", Brand = "T", Model = "M" });
            _context.Services.Add(new Service { ServiceId = 1, ServiceName = "Wash" });

            var targetDate = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            
            var b1 = new Booking
            {
                BookingId = 1, UserId = userId, BusinessProfileId = businessProfileId, BookingType = "Business", Status = "Completed", FinalAmount = 200000, 
                ScheduledTime = targetDate, LicensePlate = "51F-12345", BranchId = 1, OriginalPrice = 200000
            };
            var b2 = new Booking
            {
                BookingId = 2, UserId = userId, BusinessProfileId = businessProfileId, BookingType = "Business", Status = "Completed", FinalAmount = 350000, 
                ScheduledTime = targetDate.AddDays(1), LicensePlate = "51F-12345", BranchId = 1, OriginalPrice = 350000
            };
            
            _context.Bookings.AddRange(b1, b2);

            _context.BookingDetails.Add(new BookingDetail { DetailId = 1, BookingId = 1, ServiceId = 1, Price = 200000 });
            _context.BookingDetails.Add(new BookingDetail { DetailId = 2, BookingId = 2, ServiceId = 1, Price = 350000 });

            _context.FleetWashLogs.Add(new FleetWashLog { FleetWashLogId = 1, FleetVehicleId = 1, BranchId = 1, BookingId = 1, Status = "Completed", CompletedTime = targetDate });
            _context.FleetWashLogs.Add(new FleetWashLog { FleetWashLogId = 2, FleetVehicleId = 1, BranchId = 1, BookingId = 2, Status = "Completed", CompletedTime = targetDate.AddDays(1) });

            await _context.SaveChangesAsync();

            // Act
            int invoiceId = await _businessService.GenerateMonthlyInvoiceAsync(businessProfileId, 2026, 6);

            // Assert
            Assert.True(invoiceId > 0);

            var invoice = await _context.Invoices.Include(i => i.InvoiceItems).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            Assert.NotNull(invoice);
            Assert.Equal(businessProfileId, invoice.BusinessProfileId);
            Assert.Equal(550000, invoice.TotalAmount);
            Assert.Equal(2, invoice.InvoiceItems.Count);
        }
    }
}
