using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    public class BookingMaterialUsageServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly BookingMaterialUsageService _sut;

        public BookingMaterialUsageServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _sut = new BookingMaterialUsageService(_dbContext);
        }

        private async Task<(Branch branch, Warehouse warehouse, VehicleType vehicleType, Service service, Material material)> SeedFullSetup(
            decimal baseQuantity = 2, decimal batchRemaining = 100, bool allowNegativeStock = false)
        {
            var branch = new Branch { Name = "Branch A", IsActive = true, AllowNegativeStock = allowNegativeStock };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho Branch A", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);

            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);

            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);

            var material = new Material { Name = "Shampoo", Category = "Chemical", Unit = "L", IsActive = true, DefaultMinStockLevel = 5 };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage
            {
                ServiceId = service.ServiceId,
                VehicleTypeId = vehicleType.Id,
                MaterialId = material.MaterialId,
                BaseQuantity = baseQuantity,
                Unit = "L",
                IsActive = true
            });

            _dbContext.MaterialBatches.Add(new MaterialBatch
            {
                MaterialId = material.MaterialId,
                WarehouseId = warehouse.WarehouseId,
                BatchCode = "B001",
                ImportedQuantity = 100,
                RemainingQuantity = batchRemaining,
                UnitCost = 5000,
                TotalCost = 500000,
                Status = "Active",
                ImportedAt = DateTime.UtcNow.AddDays(-5)
            });

            _dbContext.WarehouseStocks.Add(new WarehouseStock
            {
                WarehouseId = warehouse.WarehouseId,
                MaterialId = material.MaterialId,
                CurrentQuantity = batchRemaining,
                UpdatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return (branch, warehouse, vehicleType, service, material);
        }

        private async Task<Booking> SeedCompletedBooking(Branch branch, VehicleType vehicleType, Service service, string status = "Completed")
        {
            var vehicle = new Vehicle { LicensePlate = "51Y11111", VehicleTypeId = vehicleType.Id };
            _dbContext.Vehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                LicensePlate = "51Y11111",
                VehicleId = vehicle.Id,
                BranchId = branch.BranchId,
                Status = status,
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            return booking;
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_AlreadyConsumed_ReturnsEarlyNoOp()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup();
            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            _dbContext.BookingMaterialUsages.Add(new BookingMaterialUsage
            {
                BookingId = booking.BookingId,
                BranchId = branch.BranchId,
                MaterialId = material.MaterialId,
                QuantityUsed = 2,
                UnitCost = 5000,
                CostAmount = 10000,
                UsageType = "Standard"
            });
            await _dbContext.SaveChangesAsync();

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            var count = await _dbContext.BookingMaterialUsages.CountAsync(u => u.BookingId == booking.BookingId);
            Assert.Equal(1, count); // unchanged — no duplicate consumption
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_BookingNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ConsumeForCompletedBookingAsync(999));
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_NotCompleted_ThrowsBadRequestException()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup();
            var booking = await SeedCompletedBooking(branch, vehicleType, service, status: "CheckedIn");

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConsumeForCompletedBookingAsync(booking.BookingId));
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_NoServiceMaterialUsages_ReturnsEarlyNoOp()
        {
            var branch = new Branch { Name = "Branch B", IsActive = true };
            _dbContext.Branches.Add(branch);
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            await _dbContext.SaveChangesAsync();

            var booking = await SeedCompletedBooking(branch, vehicleType, service);
            // No ServiceMaterialUsage seeded — plannedUsages will be empty

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            var count = await _dbContext.BookingMaterialUsages.CountAsync(u => u.BookingId == booking.BookingId);
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_ValidSingleBatch_ConsumesCorrectQuantityAndCost()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(baseQuantity: 2, batchRemaining: 100);
            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            // Clean condition (default) => multiplier 1.0; weight multiplier = BaseWeight(3) since > 1
            // Expected quantity = 2 * 1.0 * 3 = 6
            var usage = await _dbContext.BookingMaterialUsages.FirstOrDefaultAsync(u => u.BookingId == booking.BookingId);
            Assert.NotNull(usage);
            Assert.Equal(6m, usage.QuantityUsed);
            Assert.Equal(5000, usage.UnitCost);
            Assert.Equal(30000, usage.CostAmount); // 6 * 5000
            Assert.False(usage.IsCostPending);

            var stock = await _dbContext.WarehouseStocks.FirstAsync(s => s.WarehouseId == warehouse.WarehouseId);
            Assert.Equal(94, stock.CurrentQuantity); // 100 - 6

            var batch = await _dbContext.MaterialBatches.FirstAsync(b => b.WarehouseId == warehouse.WarehouseId);
            Assert.Equal(94, batch.RemainingQuantity);
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_DirtyCondition_AppliesConditionMultiplier()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(baseQuantity: 2, batchRemaining: 100);
            var vehicle = new Vehicle { LicensePlate = "51Y22222", VehicleTypeId = vehicleType.Id };
            _dbContext.Vehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking
            {
                LicensePlate = "51Y22222",
                VehicleId = vehicle.Id,
                BranchId = branch.BranchId,
                Status = "Completed",
                VehicleCondition = VehicleCondition.Dirty,
                ScheduledTime = DateTime.UtcNow,
                OriginalPrice = 100000,
                FinalAmount = 100000,
                BookingDetails = new List<BookingDetail> { new BookingDetail { ServiceId = service.ServiceId, Price = 100000 } }
            };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            // Dirty default multiplier 1.5 (no override configured) * weight 3 * base 2 = 9
            var usage = await _dbContext.BookingMaterialUsages.FirstAsync(u => u.BookingId == booking.BookingId);
            Assert.Equal(9m, usage.QuantityUsed);
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_InsufficientStock_NoNegativeAllowed_ThrowsBadRequestException()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(baseQuantity: 2, batchRemaining: 1, allowNegativeStock: false);
            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConsumeForCompletedBookingAsync(booking.BookingId));
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_InsufficientStock_NegativeAllowed_ConsumesIntoNegative()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(baseQuantity: 2, batchRemaining: 1, allowNegativeStock: true);
            branch.NegativeStockLimit = 100; // generous limit so it doesn't block
            await _dbContext.SaveChangesAsync();

            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            // Needed 6, batch only has 1 — 1 from batch (standard), 5 as negative-stock estimated usage
            var usages = await _dbContext.BookingMaterialUsages.Where(u => u.BookingId == booking.BookingId).ToListAsync();
            Assert.Equal(2, usages.Count);
            Assert.Contains(usages, u => u.IsCostPending); // the overflow entry
            Assert.Contains(usages, u => !u.IsCostPending); // the batch-backed entry
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_NegativeStockLimitExceeded_ThrowsBadRequestException()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(baseQuantity: 2, batchRemaining: 1, allowNegativeStock: true);
            branch.NegativeStockLimit = 1; // very tight limit — projected usage will exceed it
            await _dbContext.SaveChangesAsync();

            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConsumeForCompletedBookingAsync(booking.BookingId));
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_NoWarehouseYet_AllowNegativeStock_CreatesWarehouse()
        {
            var branch = new Branch { Name = "New Branch", IsActive = true, AllowNegativeStock = true, NegativeStockLimit = 1000 };
            _dbContext.Branches.Add(branch);
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            var material = new Material { Name = "Wax", Category = "Chemical", Unit = "L", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage
            {
                ServiceId = service.ServiceId,
                VehicleTypeId = vehicleType.Id,
                MaterialId = material.MaterialId,
                BaseQuantity = 1,
                Unit = "L",
                IsActive = true
            });
            await _dbContext.SaveChangesAsync();
            // Deliberately no Warehouse, no MaterialBatch, no WarehouseStock seeded

            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            var warehouse = await _dbContext.Warehouses.FirstOrDefaultAsync(w => w.BranchId == branch.BranchId);
            Assert.NotNull(warehouse);
            Assert.Equal($"Kho {branch.Name}", warehouse.Name);

            var usage = await _dbContext.BookingMaterialUsages.FirstOrDefaultAsync(u => u.BookingId == booking.BookingId);
            Assert.NotNull(usage);
            Assert.True(usage.IsCostPending); // no batch existed, fully overflow
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_NoWarehouseYet_NegativeStockNotAllowed_ThrowsBadRequestException()
        {
            var branch = new Branch { Name = "Strict Branch", IsActive = true, AllowNegativeStock = false };
            _dbContext.Branches.Add(branch);
            var vehicleType = new VehicleType { Name = "Sedan", BaseWeight = 3 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Wash", IsActive = true };
            _dbContext.Services.Add(service);
            var material = new Material { Name = "Wax", Category = "Chemical", Unit = "L", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage
            {
                ServiceId = service.ServiceId,
                VehicleTypeId = vehicleType.Id,
                MaterialId = material.MaterialId,
                BaseQuantity = 1,
                Unit = "L",
                IsActive = true
            });
            await _dbContext.SaveChangesAsync();

            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ConsumeForCompletedBookingAsync(booking.BookingId));
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_VehicleTypeFallsBackToVehicleWhenActualVehicleTypeIdMissing()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(baseQuantity: 2, batchRemaining: 100);
            var booking = await SeedCompletedBooking(branch, vehicleType, service);
            // booking.ActualVehicleTypeId is null by default, so it should resolve via booking.Vehicle.VehicleTypeId

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            var usage = await _dbContext.BookingMaterialUsages.FirstOrDefaultAsync(u => u.BookingId == booking.BookingId);
            Assert.NotNull(usage); // succeeded, proving vehicle type resolved correctly
        }

        [Fact]
        public async Task ConsumeForCompletedBookingAsync_DefaultUsageAppliedWhenNoVehicleSpecificOverride()
        {
            var branch = new Branch { Name = "Branch C", IsActive = true };
            _dbContext.Branches.Add(branch);
            var vehicleType = new VehicleType { Name = "SUV", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            var service = new Service { ServiceName = "Premium Wash", IsActive = true };
            _dbContext.Services.Add(service);
            var material = new Material { Name = "Foam", Category = "Chemical", Unit = "L", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var warehouse = new Warehouse { Name = "Kho Branch C", Type = "Branch", BranchId = branch.BranchId, IsActive = true };
            _dbContext.Warehouses.Add(warehouse);
            await _dbContext.SaveChangesAsync();

            // Default usage (VehicleTypeId == null) — no vehicle-specific override exists
            _dbContext.ServiceMaterialUsages.Add(new ServiceMaterialUsage
            {
                ServiceId = service.ServiceId,
                VehicleTypeId = null,
                MaterialId = material.MaterialId,
                BaseQuantity = 1,
                Unit = "L",
                IsActive = true
            });

            _dbContext.MaterialBatches.Add(new MaterialBatch
            {
                MaterialId = material.MaterialId,
                WarehouseId = warehouse.WarehouseId,
                BatchCode = "B002",
                ImportedQuantity = 50,
                RemainingQuantity = 50,
                UnitCost = 3000,
                TotalCost = 150000,
                Status = "Active",
                ImportedAt = DateTime.UtcNow
            });
            _dbContext.WarehouseStocks.Add(new WarehouseStock { WarehouseId = warehouse.WarehouseId, MaterialId = material.MaterialId, CurrentQuantity = 50, UpdatedAt = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync();

            var booking = await SeedCompletedBooking(branch, vehicleType, service);

            await _sut.ConsumeForCompletedBookingAsync(booking.BookingId);

            var usage = await _dbContext.BookingMaterialUsages.FirstOrDefaultAsync(u => u.BookingId == booking.BookingId);
            Assert.NotNull(usage); // default usage was picked up despite no exact vehicle-type match
        }

        [Fact]
        public async Task CreateExtraUsageRequestAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var dto = new ReportExtraMaterialUsageDTO { MaterialId = 1, Quantity = 2, Note = "spill" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateExtraUsageRequestAsync(999, 1, dto));
        }

        [Fact]
        public async Task CreateExtraUsageRequestAsync_NotAssignedStaff_ThrowsForbiddenException()
        {
            var branch = new Branch { Name = "Branch D", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking { LicensePlate = "51Y33333", Status = "Processing", BranchId = branch.BranchId, ProcessingStaffId = 5, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new ReportExtraMaterialUsageDTO { MaterialId = 1, Quantity = 2, Note = "spill" };

            await Assert.ThrowsAsync<ForbiddenException>(() => _sut.CreateExtraUsageRequestAsync(booking.BookingId, 999, dto));
        }

        [Fact]
        public async Task CreateExtraUsageRequestAsync_WrongBookingStatus_ThrowsBadRequestException()
        {
            var branch = new Branch { Name = "Branch E", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking { LicensePlate = "51Y44444", Status = "Pending", BranchId = branch.BranchId, ProcessingStaffId = 5, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new ReportExtraMaterialUsageDTO { MaterialId = 1, Quantity = 2, Note = "spill" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateExtraUsageRequestAsync(booking.BookingId, 5, dto));
        }

        [Fact]
        public async Task CreateExtraUsageRequestAsync_MaterialNotFound_ThrowsNotFoundException()
        {
            var branch = new Branch { Name = "Branch F", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking { LicensePlate = "51Y55555", Status = "Processing", BranchId = branch.BranchId, ProcessingStaffId = 5, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new ReportExtraMaterialUsageDTO { MaterialId = 999, Quantity = 2, Note = "spill" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateExtraUsageRequestAsync(booking.BookingId, 5, dto));
        }

        [Fact]
        public async Task CreateExtraUsageRequestAsync_InactiveMaterial_ThrowsBadRequestException()
        {
            var branch = new Branch { Name = "Branch G", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Old Wax", Category = "Chemical", Unit = "L", IsActive = false };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking { LicensePlate = "51Y66666", Status = "Processing", BranchId = branch.BranchId, ProcessingStaffId = 5, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new ReportExtraMaterialUsageDTO { MaterialId = material.MaterialId, Quantity = 2, Note = "spill" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateExtraUsageRequestAsync(booking.BookingId, 5, dto));
        }

        [Fact]
        public async Task CreateExtraUsageRequestAsync_Valid_CreatesPendingRequest()
        {
            var branch = new Branch { Name = "Branch H", IsActive = true };
            _dbContext.Branches.Add(branch);
            var material = new Material { Name = "Wax", Category = "Chemical", Unit = "L", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var booking = new Booking { LicensePlate = "51Y77777", Status = "Processing", BranchId = branch.BranchId, ProcessingStaffId = 5, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync();

            var dto = new ReportExtraMaterialUsageDTO { MaterialId = material.MaterialId, Quantity = 3, Note = "extra spill" };
            var result = await _sut.CreateExtraUsageRequestAsync(booking.BookingId, 5, dto);

            Assert.Equal("Pending", result.Status);
            Assert.Equal(3, result.Quantity);
        }

        [Fact]
        public async Task GetManagerExtraUsageRequestsAsync_ManagerNotAssignedToBranch_ThrowsBadRequestException()
        {
            await Assert.ThrowsAsync<BadRequestException>(() => _sut.GetManagerExtraUsageRequestsAsync(999));
        }

        [Fact]
        public async Task GetManagerExtraUsageRequestsAsync_FiltersToManagerBranch()
        {
            var branch = new Branch { Name = "Branch I", IsActive = true };
            _dbContext.Branches.Add(branch);
            var otherBranch = new Branch { Name = "Branch J", IsActive = true };
            _dbContext.Branches.Add(otherBranch);
            var material = new Material { Name = "Wax", Category = "Chemical", Unit = "L", IsActive = true };
            _dbContext.Materials.Add(material);
            await _dbContext.SaveChangesAsync();

            var managerUser = new User { PhoneNumber = "0999200001", Email = "mgr2@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager I", BranchId = branch.BranchId });

            var booking1 = new Booking { LicensePlate = "51Y88881", Status = "Processing", BranchId = branch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            var booking2 = new Booking { LicensePlate = "51Y88882", Status = "Processing", BranchId = otherBranch.BranchId, ScheduledTime = DateTime.UtcNow, OriginalPrice = 0, FinalAmount = 0 };
            _dbContext.Bookings.AddRange(booking1, booking2);
            await _dbContext.SaveChangesAsync();

            var staffUser = new User { PhoneNumber = "0999200002", Email = "staff3@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();

            _dbContext.ExtraMaterialUsageRequests.AddRange(
                new ExtraMaterialUsageRequest { BookingId = booking1.BookingId, StaffUserId = staffUser.UserId, BranchId = branch.BranchId, MaterialId = material.MaterialId, Quantity = 1, Status = "Pending" },
                new ExtraMaterialUsageRequest { BookingId = booking2.BookingId, StaffUserId = staffUser.UserId, BranchId = otherBranch.BranchId, MaterialId = material.MaterialId, Quantity = 1, Status = "Pending" }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetManagerExtraUsageRequestsAsync(managerUser.UserId);

            Assert.Single(result);
            Assert.Equal(branch.BranchId, result[0].BranchId);
        }

        [Fact]
        public async Task ApproveExtraUsageRequestAsync_RequestNotFound_ThrowsNotFoundException()
        {
            var branch = new Branch { Name = "Branch K", IsActive = true };
            _dbContext.Branches.Add(branch);
            var managerUser = new User { PhoneNumber = "0999200003", Email = "mgr3@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager K", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewExtraMaterialUsageRequestDTO { ManagerNote = "ok" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ApproveExtraUsageRequestAsync(managerUser.UserId, 999, dto));
        }

        [Fact]
        public async Task ApproveExtraUsageRequestAsync_WrongBranch_ThrowsForbiddenException()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup();
            var otherBranch = new Branch { Name = "Branch L", IsActive = true };
            _dbContext.Branches.Add(otherBranch);
            var managerUser = new User { PhoneNumber = "0999200004", Email = "mgr4@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager L", BranchId = otherBranch.BranchId });

            var booking = await SeedCompletedBooking(branch, vehicleType, service, status: "Processing");
            var staffUser = new User { PhoneNumber = "0999200005", Email = "staff4@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();

            var request = new ExtraMaterialUsageRequest { BookingId = booking.BookingId, StaffUserId = staffUser.UserId, BranchId = branch.BranchId, MaterialId = material.MaterialId, Quantity = 1, Status = "Pending" };
            _dbContext.ExtraMaterialUsageRequests.Add(request);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewExtraMaterialUsageRequestDTO { ManagerNote = "ok" };

            await Assert.ThrowsAsync<ForbiddenException>(() => _sut.ApproveExtraUsageRequestAsync(managerUser.UserId, request.RequestId, dto));
        }

        [Fact]
        public async Task ApproveExtraUsageRequestAsync_NotPending_ThrowsBadRequestException()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup();
            var managerUser = new User { PhoneNumber = "0999200006", Email = "mgr5@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager M", BranchId = branch.BranchId });

            var booking = await SeedCompletedBooking(branch, vehicleType, service, status: "Processing");
            var staffUser = new User { PhoneNumber = "0999200007", Email = "staff5@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();

            var request = new ExtraMaterialUsageRequest { BookingId = booking.BookingId, StaffUserId = staffUser.UserId, BranchId = branch.BranchId, MaterialId = material.MaterialId, Quantity = 1, Status = "Approved" };
            _dbContext.ExtraMaterialUsageRequests.Add(request);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewExtraMaterialUsageRequestDTO { ManagerNote = "ok" };

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ApproveExtraUsageRequestAsync(managerUser.UserId, request.RequestId, dto));
        }

        [Fact]
        public async Task ApproveExtraUsageRequestAsync_Valid_ConsumesMaterialAndApproves()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup(batchRemaining: 100);
            var managerUser = new User { PhoneNumber = "0999200008", Email = "mgr6@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager N", BranchId = branch.BranchId });

            var booking = await SeedCompletedBooking(branch, vehicleType, service, status: "Processing");
            var staffUser = new User { PhoneNumber = "0999200009", Email = "staff6@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();

            var request = new ExtraMaterialUsageRequest { BookingId = booking.BookingId, StaffUserId = staffUser.UserId, BranchId = branch.BranchId, MaterialId = material.MaterialId, Quantity = 5, Status = "Pending" };
            _dbContext.ExtraMaterialUsageRequests.Add(request);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewExtraMaterialUsageRequestDTO { ManagerNote = "approved, valid reason" };
            var result = await _sut.ApproveExtraUsageRequestAsync(managerUser.UserId, request.RequestId, dto);

            Assert.Equal("Approved", result.Status);

            var extraUsage = await _dbContext.BookingMaterialUsages.FirstOrDefaultAsync(u => u.BookingId == booking.BookingId && u.UsageType == "Extra");
            Assert.NotNull(extraUsage);
            Assert.Equal(5, extraUsage.QuantityUsed);
        }

        [Fact]
        public async Task RejectExtraUsageRequestAsync_RequestNotFound_ThrowsNotFoundException()
        {
            var branch = new Branch { Name = "Branch O", IsActive = true };
            _dbContext.Branches.Add(branch);
            var managerUser = new User { PhoneNumber = "0999200010", Email = "mgr7@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager O", BranchId = branch.BranchId });
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewExtraMaterialUsageRequestDTO { ManagerNote = "no" };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RejectExtraUsageRequestAsync(managerUser.UserId, 999, dto));
        }

        [Fact]
        public async Task RejectExtraUsageRequestAsync_Valid_SetsRejectedStatus()
        {
            var (branch, warehouse, vehicleType, service, material) = await SeedFullSetup();
            var managerUser = new User { PhoneNumber = "0999200011", Email = "mgr8@test.com", PasswordHash = "x", Role = "Manager", Status = "Active" };
            _dbContext.Users.Add(managerUser);
            await _dbContext.SaveChangesAsync();
            _dbContext.EmployeeProfiles.Add(new EmployeeProfile { EmployeeId = managerUser.UserId, FullName = "Manager P", BranchId = branch.BranchId });

            var booking = await SeedCompletedBooking(branch, vehicleType, service, status: "Processing");
            var staffUser = new User { PhoneNumber = "0999200012", Email = "staff7@test.com", PasswordHash = "x", Role = "Staff", Status = "Active" };
            _dbContext.Users.Add(staffUser);
            await _dbContext.SaveChangesAsync();

            var request = new ExtraMaterialUsageRequest { BookingId = booking.BookingId, StaffUserId = staffUser.UserId, BranchId = branch.BranchId, MaterialId = material.MaterialId, Quantity = 5, Status = "Pending" };
            _dbContext.ExtraMaterialUsageRequests.Add(request);
            await _dbContext.SaveChangesAsync();

            var dto = new ReviewExtraMaterialUsageRequestDTO { ManagerNote = "unjustified" };
            var result = await _sut.RejectExtraUsageRequestAsync(managerUser.UserId, request.RequestId, dto);

            Assert.Equal("Rejected", result.Status);
            var noExtraUsage = await _dbContext.BookingMaterialUsages.AnyAsync(u => u.BookingId == booking.BookingId && u.UsageType == "Extra");
            Assert.False(noExtraUsage); // rejected — no material actually consumed
        }
    }
}