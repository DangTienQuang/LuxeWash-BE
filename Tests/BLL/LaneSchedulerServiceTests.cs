using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using BLL.DTOs.Business;
using BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class LaneSchedulerServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly LaneSchedulerService _sut;

        public LaneSchedulerServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new LaneSchedulerService(_dbContext);
        }

        private async Task<(VehicleType vehicleType, Service service)> SeedVehicleTypeWithService(int branchId, int estimatedMinutes)
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var svc = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(svc);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServicePrices.Add(new ServicePrice { ServiceId = svc.ServiceId, VehicleTypeId = vehicleType.Id, BranchId = branchId, Price = 100000, EstimatedDurationMinutes = estimatedMinutes });
            await _dbContext.SaveChangesAsync();

            return (vehicleType, svc);
        }

        [Fact]
        public async Task GetLaneProjectedFreeTimesAsync_NoActiveLanes_ReturnsEmptyDict()
        {
            var result = await _sut.GetLaneProjectedFreeTimesAsync(1, DateTime.UtcNow);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetLaneProjectedFreeTimesAsync_IdleLane_FreeAtSlotStart()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var slotStart = DateTime.UtcNow.AddDays(1);
            var result = await _sut.GetLaneProjectedFreeTimesAsync(1, slotStart);

            Assert.Equal(slotStart, result[lane.LaneId]);
        }

        [Fact]
        public async Task GetLaneProjectedFreeTimesAsync_BusyLane_FreeAtCheckInPlusEstimate()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 30);
            var fleetVehicle = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51W11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            var checkInTime = DateTime.UtcNow.AddMinutes(-10);
            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = 1, LaneId = lane.LaneId, CheckInTime = checkInTime, Status = "Processing", WashCost = 0 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetLaneProjectedFreeTimesAsync(1, DateTime.UtcNow);

            Assert.Equal(checkInTime.AddMinutes(30), result[lane.LaneId]);
        }

        [Fact]
        public async Task ScheduleFleetAsync_EmptyVehicleList_ReturnsFail()
        {
            var result = await _sut.ScheduleFleetAsync(1, DateTime.UtcNow, TimeSpan.FromHours(1), new List<VehicleScheduleRequest>());

            Assert.False(result.Success);
            Assert.Equal("Vehicle list cannot be empty.", result.ErrorMessage);
        }

        [Fact]
        public async Task ScheduleFleetAsync_NoBusinessLanes_ReturnsFail()
        {
            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 20);
            var vehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 1, VehicleType = vehicleType, ServicePrices = await _dbContext.ServicePrices.ToListAsync() }
            };

            var result = await _sut.ScheduleFleetAsync(1, DateTime.UtcNow, TimeSpan.FromHours(1), vehicles);

            Assert.False(result.Success);
            Assert.Equal("No available lane in this branch.", result.ErrorMessage);
        }

        [Fact]
        public async Task ScheduleFleetAsync_SingleVehicle_IdleLane_StartsAtSlotStart()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 20);
            var servicePrices = await _dbContext.ServicePrices.ToListAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
            var vehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 1, VehicleType = vehicleType, ServicePrices = servicePrices }
            };

            var result = await _sut.ScheduleFleetAsync(1, slotStart, TimeSpan.FromHours(1), vehicles);

            Assert.True(result.Success);
            Assert.Single(result.Assignments);
            Assert.Equal(slotStart, result.Assignments[0].EstimatedStart);
            Assert.Equal(slotStart.AddMinutes(20), result.Assignments[0].EstimatedEnd);
        }

        [Fact]
        public async Task ScheduleFleetAsync_LaneOccupied_NewVehicleStartsAfterLaneFrees()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 20);
            var fleetVehicle = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51W22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(fleetVehicle);
            await _dbContext.SaveChangesAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
            var checkInTime = slotStart.AddMinutes(-5);
            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = fleetVehicle.FleetVehicleId, BranchId = 1, LaneId = lane.LaneId, CheckInTime = checkInTime, Status = "Processing", WashCost = 0 });
            await _dbContext.SaveChangesAsync();

            var servicePrices = await _dbContext.ServicePrices.ToListAsync();
            var vehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 99, VehicleType = vehicleType, ServicePrices = servicePrices }
            };

            var result = await _sut.ScheduleFleetAsync(1, slotStart, TimeSpan.FromHours(1), vehicles);

            Assert.True(result.Success);
            // Lane frees at checkInTime + 20 min = slotStart + 15 min, which is > slotStart, so new vehicle starts there
            Assert.Equal(checkInTime.AddMinutes(20), result.Assignments[0].EstimatedStart);
        }

        [Fact]
        public async Task ScheduleFleetAsync_TwoVehiclesTwoLanes_AssignedToEarliestFreeLaneEach()
        {
            var lane1 = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            var lane2 = new Lane { BranchId = 1, Name = "Lane 2", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.AddRange(lane1, lane2);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 20);
            var servicePrices = await _dbContext.ServicePrices.ToListAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
            var vehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 1, VehicleType = vehicleType, ServicePrices = servicePrices },
                new VehicleScheduleRequest { FleetVehicleId = 2, VehicleType = vehicleType, ServicePrices = servicePrices }
            };

            var result = await _sut.ScheduleFleetAsync(1, slotStart, TimeSpan.FromHours(1), vehicles);

            Assert.True(result.Success);
            Assert.Equal(2, result.Assignments.Count);
            // Both lanes idle at slotStart, so both vehicles should start simultaneously on different lanes
            Assert.Equal(slotStart, result.Assignments[0].EstimatedStart);
            Assert.Equal(slotStart, result.Assignments[1].EstimatedStart);
            Assert.NotEqual(result.Assignments[0].LaneId, result.Assignments[1].LaneId);
        }

        [Fact]
        public async Task ScheduleFleetAsync_ThirdVehicleSameLane_StartsAfterBufferFromFirst()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 20);
            var servicePrices = await _dbContext.ServicePrices.ToListAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
            var vehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 1, VehicleType = vehicleType, ServicePrices = servicePrices },
                new VehicleScheduleRequest { FleetVehicleId = 2, VehicleType = vehicleType, ServicePrices = servicePrices }
            };

            var result = await _sut.ScheduleFleetAsync(1, slotStart, TimeSpan.FromHours(3), vehicles);

            Assert.True(result.Success);
            // Second vehicle on same lane starts after first's end + 2 min buffer
            Assert.Equal(slotStart.AddMinutes(20 + 2), result.Assignments[1].EstimatedStart);
        }

        [Fact]
        public async Task ScheduleFleetAsync_ExceedsDeadline_ReturnsFail()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 50); // long wash time
            var servicePrices = await _dbContext.ServicePrices.ToListAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
            var vehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 1, VehicleType = vehicleType, ServicePrices = servicePrices }
            };

            // 30 min slot + 15 min grace = 45 min deadline, but wash takes 50 min — should fail
            var result = await _sut.ScheduleFleetAsync(1, slotStart, TimeSpan.FromMinutes(30), vehicles);

            Assert.False(result.Success);
            Assert.Contains("Not enough time", result.ErrorMessage);
        }

        [Fact]
        public async Task ScheduleFleetAsync_ExistingPendingBooking_OccupiesLane()
        {
            var lane = new Lane { BranchId = 1, Name = "Lane 1", IsActive = true, IsBusinessLane = true };
            _dbContext.Lanes.Add(lane);
            await _dbContext.SaveChangesAsync();

            var (vehicleType, service) = await SeedVehicleTypeWithService(1, 20);
            var existingFleetVehicle = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51W33333", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(existingFleetVehicle);
            await _dbContext.SaveChangesAsync();

            var slotStart = DateTime.UtcNow.AddDays(1).Date.AddHours(9);

            var existingBooking = new Booking
            {
                BranchId = 1,
                BookingType = "Business",
                Status = "Pending",
                LicensePlate = "51W33333",
                FleetVehicleId = existingFleetVehicle.FleetVehicleId,
                ProcessingLaneId = lane.LaneId,
                ScheduledTime = slotStart,
                OriginalPrice = 0,
                FinalAmount = 0,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(existingBooking);
            await _dbContext.SaveChangesAsync();

            var servicePrices = await _dbContext.ServicePrices.ToListAsync();
            var newVehicles = new List<VehicleScheduleRequest>
            {
                new VehicleScheduleRequest { FleetVehicleId = 99, VehicleType = vehicleType, ServicePrices = servicePrices }
            };

            var result = await _sut.ScheduleFleetAsync(1, slotStart, TimeSpan.FromHours(2), newVehicles);

            Assert.True(result.Success);
            // New vehicle must start after the existing booking's estimated end + buffer (20 + 2 = 22 min after slotStart)
            Assert.Equal(slotStart.AddMinutes(22), result.Assignments[0].EstimatedStart);
        }
    }
}