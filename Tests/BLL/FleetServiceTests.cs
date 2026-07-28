using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs.Fleet;
using BLL.Services;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class FleetServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<ICloudinaryService> _cloudinaryMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly FleetService _sut;

        public FleetServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _cloudinaryMock = new Mock<ICloudinaryService>();
            _configMock = new Mock<IConfiguration>();
            _sut = new FleetService(_dbContext, _cloudinaryMock.Object, _configMock.Object);
        }

        private async Task<(User user, BusinessProfile business)> SeedApprovedBusiness()
        {
            var user = new User { PhoneNumber = "0999950" + new Random().Next(100, 999), Email = $"fleet{Guid.NewGuid()}@test.com", PasswordHash = "x", Role = "Business", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var business = new BusinessProfile
            {
                UserId = user.UserId,
                CompanyName = "Fleet Co",
                ApprovalStatus = "Approved",
                IsContractActive = true,
                BusinessLicenseFileUrl = "x",
                CreatedAt = DateTime.UtcNow,
                ContractStartDate = DateTime.UtcNow,
                ContractEndDate = DateTime.UtcNow.AddYears(1)
            };
            _dbContext.BusinessProfiles.Add(business);
            await _dbContext.SaveChangesAsync();

            return (user, business);
        }

        static FleetServiceTests()
        {
            ExcelPackage.License.SetNonCommercialPersonal("AutoWashPro Tests");
        }

        private IFormFile BuildExcelFile(List<string[]> rows)
        {
            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("Sheet1");

            // Header row (row 1) — content doesn't matter, import starts reading at row 2
            sheet.Cells[1, 1].Value = "Index";
            sheet.Cells[1, 2].Value = "LicensePlate";
            sheet.Cells[1, 3].Value = "VehicleType";
            sheet.Cells[1, 4].Value = "Brand";
            sheet.Cells[1, 5].Value = "Model";
            sheet.Cells[1, 6].Value = "DriverName";
            sheet.Cells[1, 7].Value = "EmployeeCode";

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                for (int col = 0; col < row.Length; col++)
                {
                    sheet.Cells[i + 2, col + 2].Value = row[col]; // col+2 because column 1 (Index) is unused by the import logic
                }
            }

            var bytes = package.GetAsByteArray();
            var stream = new MemoryStream(bytes);

            var formFile = new FormFile(stream, 0, stream.Length, "file", "fleet.xlsx")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
            return formFile;
        }

        [Fact]
        public async Task GetImportBatchDetailAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetImportBatchDetailAsync(999));
        }

        [Fact]
        public async Task GetImportBatchDetailAsync_Valid_ReturnsWithErrors()
        {
            var batch = new FleetImportBatch { BusinessProfileId = 1, FileUrl = "x", Status = "PartialSuccess", TotalRows = 5, SuccessRows = 3, FailedRows = 2, CreatedAt = DateTime.UtcNow };
            _dbContext.FleetImportBatches.Add(batch);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetImportErrors.Add(new FleetImportError { FleetImportBatchId = batch.FleetImportBatchId, RowNumber = 3, ErrorMessage = "Bad plate" });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetImportBatchDetailAsync(batch.FleetImportBatchId);

            Assert.Single(result.Errors);
            Assert.Equal("Bad plate", result.Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task GetPendingVehiclesAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetPendingVehiclesAsync(999));
        }

        [Fact]
        public async Task GetPendingVehiclesAsync_ReturnsOnlyPendingForBusiness()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetVehicles.AddRange(
                new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Y11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 },
                new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Y22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetPendingVehiclesAsync(user.UserId);

            Assert.Single(result);
            Assert.Equal("51Y11111", result[0].LicensePlate);
        }

        [Fact]
        public async Task GetAllPendingVehiclesAsync_FiltersByBusinessProfileId()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetVehicles.Add(new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Y33333", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetAllPendingVehiclesAsync(business.BusinessProfileId);

            Assert.Single(result);
            Assert.Equal("Fleet Co", result[0].BusinessName);
        }

        [Fact]
        public async Task ApproveFleetVehicleAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.ApproveFleetVehicleAsync(999));
        }

        [Fact]
        public async Task ApproveFleetVehicleAsync_Valid_SetsActive()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            var vehicle = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51Y44444", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            await _sut.ApproveFleetVehicleAsync(vehicle.FleetVehicleId);

            var updated = await _dbContext.FleetVehicles.FirstAsync(v => v.FleetVehicleId == vehicle.FleetVehicleId);
            Assert.Equal("Active", updated.Status);
        }

        [Fact]
        public async Task RejectFleetVehicleAsync_NotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.RejectFleetVehicleAsync(999, "bad data"));
        }

        [Fact]
        public async Task RejectFleetVehicleAsync_Valid_SetsRejectedWithReason()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            var vehicle = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51Y55555", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            await _sut.RejectFleetVehicleAsync(vehicle.FleetVehicleId, "invalid plate format");

            var updated = await _dbContext.FleetVehicles.FirstAsync(v => v.FleetVehicleId == vehicle.FleetVehicleId);
            Assert.Equal("Rejected", updated.Status);
            Assert.Equal("invalid plate format", updated.RejectionReason);
        }

        [Fact]
        public async Task GetBusinessQueueAsync_ReturnsOrderedByCheckInWithPositions()
        {
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            var v1 = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51Y66666", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            var v2 = new FleetVehicle { BusinessProfileId = 1, LicensePlate = "51Y77777", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.AddRange(v1, v2);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.AddRange(
                new FleetWashLog { FleetVehicleId = v2.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "CheckedIn", WashCost = 0 },
                new FleetWashLog { FleetVehicleId = v1.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow.AddMinutes(-10), Status = "CheckedIn", WashCost = 0 }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetBusinessQueueAsync(1);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Position);
            Assert.Equal("51Y66666", result[0].LicensePlate); // earlier check-in first
        }

        [Fact]
        public async Task GetHistoryAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            var filter = new FleetHistoryFilterDTO { Page = 1, PageSize = 10 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetHistoryAsync(999, filter));
        }

        [Fact]
        public async Task GetHistoryAsync_FiltersByVehicleId()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            var v1 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Y88888", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            var v2 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Y99999", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.AddRange(v1, v2);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.AddRange(
                new FleetWashLog { FleetVehicleId = v1.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Completed", WashCost = 50000, CompletedTime = DateTime.UtcNow },
                new FleetWashLog { FleetVehicleId = v2.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.UtcNow, Status = "Completed", WashCost = 60000, CompletedTime = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var filter = new FleetHistoryFilterDTO { Page = 1, PageSize = 10, FleetVehicleId = v1.FleetVehicleId };
            var result = await _sut.GetHistoryAsync(user.UserId, filter);

            Assert.Single(result);
            Assert.Equal("51Y88888", result[0].LicensePlate);
        }

        [Fact]
        public async Task GetDashboardAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetDashboardAsync(999));
        }

        [Fact]
        public async Task GetDashboardAsync_CountsCorrectly()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            var v1 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Z11111", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            var v2 = new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Z22222", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "PendingApproval", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 };
            _dbContext.FleetVehicles.AddRange(v1, v2);
            await _dbContext.SaveChangesAsync();

            _dbContext.FleetWashLogs.Add(new FleetWashLog { FleetVehicleId = v1.FleetVehicleId, BranchId = 1, CheckInTime = DateTime.Today, Status = "Completed", WashCost = 50000 });
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetDashboardAsync(user.UserId);

            Assert.Equal(2, result.TotalVehicles);
            Assert.Equal(1, result.ActiveVehicles);
            Assert.Equal(1, result.PendingVehicles);
            Assert.Equal(1, result.TodayWashCount);
        }

        [Fact]
        public async Task GetWashHistoryAsync_BusinessNotFound_ThrowsNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetWashHistoryAsync(999));
        }

        [Fact]
        public async Task GetFleetTemplateAsync_NoUrlConfigured_ThrowsNotFoundException()
        {
            _configMock.Setup(c => c["FleetTemplate:DownloadUrl"]).Returns((string)null);

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetFleetTemplateAsync());
        }

        [Fact]
        public async Task GetFleetTemplateAsync_Valid_ReturnsUrl()
        {
            _configMock.Setup(c => c["FleetTemplate:DownloadUrl"]).Returns("https://example.com/template.xlsx");

            var result = await _sut.GetFleetTemplateAsync();

            Assert.Equal("https://example.com/template.xlsx", result.DownloadUrl);
        }

        [Fact]
        public async Task CreateBusinessLaneAsync_BranchNotFound_ThrowsNotFoundException()
        {
            var dto = new CreateBusinessLaneDTO { Name = "Lane 1", BranchId = 999 };

            await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateBusinessLaneAsync(dto));
        }

        [Fact]
        public async Task CreateBusinessLaneAsync_Valid_CreatesBusinessLane()
        {
            var branch = new Branch { Name = "Branch A", IsActive = true };
            _dbContext.Branches.Add(branch);
            await _dbContext.SaveChangesAsync();

            var dto = new CreateBusinessLaneDTO { Name = "Fleet Lane", BranchId = branch.BranchId };
            var result = await _sut.CreateBusinessLaneAsync(dto);

            Assert.True(result.IsBusinessLane);
            Assert.Equal(branch.BranchId, result.BranchId);
        }

        [Fact]
        public async Task GetImportBatchesAsync_ReturnsOrderedByCreatedAtDescending()
        {
            _dbContext.FleetImportBatches.AddRange(
                new FleetImportBatch { BusinessProfileId = 1, FileUrl = "x", Status = "Completed", CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new FleetImportBatch { BusinessProfileId = 1, FileUrl = "y", Status = "Completed", CreatedAt = DateTime.UtcNow }
            );
            await _dbContext.SaveChangesAsync();

            var result = await _sut.GetImportBatchesAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("y", result[0].FileUrl);
        }

        [Fact]
        public async Task ImportFleetAsync_NoApprovedBusiness_ThrowsBadRequestException()
        {
            var file = BuildExcelFile(new List<string[]> { new[] { "51Z00001", "Van", "Ford", "Transit", "Driver A", "EMP1" } });

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportFleetAsync(999, file));
        }

        [Fact]
        public async Task ImportFleetAsync_NullFile_ThrowsBadRequestException()
        {
            var (user, business) = await SeedApprovedBusiness();

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportFleetAsync(user.UserId, null));
        }

        [Fact]
        public async Task ImportFleetAsync_EmptyFile_ThrowsBadRequestException()
        {
            var (user, business) = await SeedApprovedBusiness();
            var emptyStream = new MemoryStream();
            var emptyFile = new FormFile(emptyStream, 0, 0, "file", "empty.xlsx");

            await Assert.ThrowsAsync<BadRequestException>(() => _sut.ImportFleetAsync(user.UserId, emptyFile));
        }

        [Fact]
        public async Task ImportFleetAsync_AllValidRows_CreatesVehiclesAndCompletes()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]>
    {
        new[] { "51Z11111", "Van", "Ford", "Transit", "Driver A", "EMP1" },
        new[] { "51Z22222", "Van", "Ford", "Transit", "Driver B", "EMP2" }
    });

            var result = await _sut.ImportFleetAsync(user.UserId, file);

            Assert.Equal("Completed", result.Status);
            Assert.Equal(2, result.SuccessRows);
            Assert.Equal(0, result.FailedRows);

            var vehicles = await _dbContext.FleetVehicles.CountAsync(v => v.BusinessProfileId == business.BusinessProfileId);
            Assert.Equal(2, vehicles);
        }

        [Fact]
        public async Task ImportFleetAsync_InvalidVehicleType_RecordsErrorAndPartialSuccess()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]>
    {
        new[] { "51Z33333", "Van", "Ford", "Transit", "Driver A", "EMP1" },
        new[] { "51Z44444", "NonexistentType", "Ford", "Transit", "Driver B", "EMP2" }
    });

            var result = await _sut.ImportFleetAsync(user.UserId, file);

            Assert.Equal("PartialSuccess", result.Status);
            Assert.Equal(1, result.SuccessRows);
            Assert.Equal(1, result.FailedRows);

            var error = await _dbContext.FleetImportErrors.FirstOrDefaultAsync(e => e.FleetImportBatchId == result.FleetImportBatchId);
            Assert.NotNull(error);
            Assert.Contains("Vehicle Type", error.ErrorMessage);
        }

        [Fact]
        public async Task ImportFleetAsync_DuplicatePlateInFile_RecordsError()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]>
    {
        new[] { "51Z55555", "Van", "Ford", "Transit", "Driver A", "EMP1" },
        new[] { "51Z55555", "Van", "Ford", "Transit", "Driver B", "EMP2" } // same plate again
    });

            var result = await _sut.ImportFleetAsync(user.UserId, file);

            Assert.Equal("PartialSuccess", result.Status);
            Assert.Equal(1, result.SuccessRows);
            Assert.Equal(1, result.FailedRows);
        }

        [Fact]
        public async Task ImportFleetAsync_PlateAlreadyExistsInSystem_RecordsError()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            _dbContext.FleetVehicles.Add(new FleetVehicle { BusinessProfileId = business.BusinessProfileId, LicensePlate = "51Z66666", VehicleTypeId = vehicleType.Id, Brand = "Ford", Model = "Transit", Status = "Active", CreatedAt = DateTime.UtcNow, FleetImportBatchId = 1 });
            await _dbContext.SaveChangesAsync();

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]> { new[] { "51Z66666", "Van", "Ford", "Transit", "Driver A", "EMP1" } });

            var result = await _sut.ImportFleetAsync(user.UserId, file);

            Assert.Equal("Failed", result.Status);
            Assert.Equal(0, result.SuccessRows);
            Assert.Equal(1, result.FailedRows);
        }

        [Fact]
        public async Task ImportFleetAsync_EmptyRowsSkipped_NotCountedAsErrors()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]>
    {
        new[] { "51Z77777", "Van", "Ford", "Transit", "Driver A", "EMP1" },
        new[] { "", "", "", "", "", "" } // fully empty row
    });

            var result = await _sut.ImportFleetAsync(user.UserId, file);

            Assert.Equal(1, result.TotalRows); // empty row not counted at all
            Assert.Equal(1, result.SuccessRows);
        }

        [Fact]
        public async Task ImportFleetAsync_MatchingActiveCarModelExists_AutoApprovesVehicle()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            _dbContext.CarModels.Add(new CarModel { Brand = "Ford", Name = "Transit", VehicleTypeId = vehicleType.Id, IsActive = true, Status = "Approved" });
            await _dbContext.SaveChangesAsync();

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]> { new[] { "51Z88888", "Van", "Ford", "Transit", "Driver A", "EMP1" } });

            await _sut.ImportFleetAsync(user.UserId, file);

            var vehicle = await _dbContext.FleetVehicles.FirstAsync(v => v.LicensePlate == "51Z88888");
            Assert.Equal("Active", vehicle.Status); // auto-approved since matching CarModel exists
        }

        [Fact]
        public async Task ImportFleetAsync_NoMatchingCarModel_StatusPendingApproval()
        {
            var (user, business) = await SeedApprovedBusiness();
            var vehicleType = new VehicleType { Name = "Van", BaseWeight = 5 };
            _dbContext.VehicleTypes.Add(vehicleType);
            await _dbContext.SaveChangesAsync();
            // No matching CarModel seeded

            _cloudinaryMock.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://cdn.example.com/file.xlsx");

            var file = BuildExcelFile(new List<string[]> { new[] { "51Z99999", "Van", "Ford", "Transit", "Driver A", "EMP1" } });

            await _sut.ImportFleetAsync(user.UserId, file);

            var vehicle = await _dbContext.FleetVehicles.FirstAsync(v => v.LicensePlate == "51Z99999");
            Assert.Equal("PendingApproval", vehicle.Status);
        }
    }
}