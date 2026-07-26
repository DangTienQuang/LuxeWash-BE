using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AutoWashPro.DAL.Data;
using DAL.Entities;
using AutoWashPro.DAL.Entities;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.Services;
using BLL.Services.Interface;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.Tests
{
    public class BusinessBookingServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly Mock<ILaneSchedulerService> _mockLaneSchedulerService;
        private readonly Mock<IBookingMaterialUsageService> _mockBookingMaterialUsageService;
        private readonly BusinessBookingService _businessBookingService;

        public BusinessBookingServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AutoWashDbContext(options);
            _mockLaneSchedulerService = new Mock<ILaneSchedulerService>(MockBehavior.Default);
            _mockBookingMaterialUsageService = new Mock<IBookingMaterialUsageService>(MockBehavior.Default);

            _businessBookingService = new BusinessBookingService(_context, _mockLaneSchedulerService.Object, _mockBookingMaterialUsageService.Object);
        }

        private async Task SeedBaseDataAsync(int userId, int businessProfileId, int branchId, int vehicleTypeId, int fleetVehicleId, int serviceId, int slotId, string licensePlate)
        {
            _context.Users.Add(new User { UserId = userId, Email = "b2badmin@company.com", PasswordHash = "hash", Role = "Business", Status = "Active", PhoneNumber = "0123456789" });
            _context.BusinessProfiles.Add(new BusinessProfile { 
                BusinessProfileId = businessProfileId, 
                UserId = userId, 
                CompanyName = "Test Fleet Co", 
                ApprovalStatus = "Approved",
                BusinessLicenseFileUrl = "http://fake.url"
            });

            _context.Branches.Add(new Branch { BranchId = branchId, Name = "Branch 1", IsActive = true });
            _context.VehicleTypes.Add(new VehicleType { Id = vehicleTypeId, Name = "SUV", BaseWeight = 1 });
            
            _context.FleetVehicles.Add(new FleetVehicle { 
                FleetVehicleId = fleetVehicleId, 
                BusinessProfileId = businessProfileId, 
                VehicleTypeId = vehicleTypeId, 
                LicensePlate = licensePlate, 
                Status = "Active",
                Brand = "Toyota",
                Model = "Innova"
            });

            _context.Services.Add(new Service { ServiceId = serviceId, ServiceName = "Wash", IsActive = true });
            _context.ServicePrices.Add(new ServicePrice { ServiceId = serviceId, VehicleTypeId = vehicleTypeId, BranchId = branchId, Price = 100000 });
            
            _context.TimeSlots.Add(new TimeSlot { SlotId = slotId, BranchId = branchId, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 0, 0), MaxCapacity = 5 });

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_HappyPath_CreatesBookings_TC10()
        {
            // Arrange
            int userId = 20, businessProfileId = 20, branchId = 20, vehicleTypeId = 20, fleetVehicleId = 20, serviceId = 20, slotId = 20;
            string licensePlate = "51F-12345";
            await SeedBaseDataAsync(userId, businessProfileId, branchId, vehicleTypeId, fleetVehicleId, serviceId, slotId, licensePlate);

            var scheduledDate = DateTime.UtcNow.Date.AddDays(1);
            
            _mockLaneSchedulerService.Setup(x => x.ScheduleFleetAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<List<VehicleScheduleRequest>>()))
                .ReturnsAsync(LaneScheduleResult.Ok(new List<VehicleAssignment> {
                    new VehicleAssignment { FleetVehicleId = fleetVehicleId, LaneId = 1, EstimatedStart = scheduledDate, EstimatedEnd = scheduledDate.AddMinutes(30) }
                }));

            var request = new CreateBusinessBookingDTO
            {
                BranchId = branchId,
                ScheduledTime = scheduledDate,
                SlotId = slotId,
                Vehicles = new List<VehicleBookingItemDTO>
                {
                    new VehicleBookingItemDTO 
                    {
                        FleetVehicleId = fleetVehicleId,
                        ServiceIds = new List<int> { serviceId }
                    }
                }
            };

            // Act
            var result = await _businessBookingService.CreateBusinessBookingAsync(userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Vehicles);
            
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == result.Vehicles.First().BookingId);
            Assert.NotNull(booking);
            Assert.Equal("Pending", booking.Status);
            Assert.Equal("Business", booking.BookingType);
        }

        [Fact]
        public async Task CreateBusinessBookingAsync_NonFleetVehicle_ThrowsException_TC11()
        {
            // Arrange
            int userId = 30, businessProfileId = 30, branchId = 30, vehicleTypeId = 30, fleetVehicleId = 30, serviceId = 30, slotId = 30;
            string licensePlate = "51F-55555";
            await SeedBaseDataAsync(userId, businessProfileId, branchId, vehicleTypeId, fleetVehicleId, serviceId, slotId, licensePlate);

            var scheduledDate = DateTime.UtcNow.Date.AddDays(1);
            var request = new CreateBusinessBookingDTO
            {
                BranchId = branchId,
                ScheduledTime = scheduledDate,
                SlotId = slotId,
                Vehicles = new List<VehicleBookingItemDTO>
                {
                    new VehicleBookingItemDTO 
                    {
                        FleetVehicleId = 999, // Unowned or non-existent fleet vehicle
                        ServiceIds = new List<int> { serviceId }
                    }
                }
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotFoundException>(() => _businessBookingService.CreateBusinessBookingAsync(userId, request));
            Assert.Contains("One or more vehicles do not belong to this business", exception.Message);
        }
    }
}
