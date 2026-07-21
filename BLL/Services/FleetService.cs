using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs.Fleet;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BLL.Services.FleetService;

namespace BLL.Services
{
    public class FleetService : IFleetService
    {
        private readonly AutoWashDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IConfiguration _configuration;

        public FleetService(AutoWashDbContext context, ICloudinaryService cloudinaryService, IConfiguration configuration)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
            _configuration = configuration;
        }

        public async Task<FleetImportResultDTO> ImportFleetAsync(int userId, IFormFile file)
        {
            var business = await _context.BusinessProfiles.FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.ApprovalStatus == "Approved");

            if (business == null)
            {
                throw new BadRequestException("Business account has not been approved.");
            }

            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("Please upload an Excel file.");
            }

            var fileUrl =
                await _cloudinaryService.UploadFileAsync(file, "fleet-imports");

            var batch = new FleetImportBatch
            {
                BusinessProfileId = business.BusinessProfileId,
                FileUrl = fileUrl,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            _context.FleetImportBatches.Add(batch);

            await _context.SaveChangesAsync();

            using var stream = new MemoryStream();

            await file.CopyToAsync(stream);

            stream.Position = 0;

            using var package = new ExcelPackage(stream);

            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet?.Dimension == null)
            {
                throw new BadRequestException("The Excel file contains no data.");
            }
            int rowCount = worksheet.Dimension.Rows;

            var importedPlates = new HashSet<string>();

            for (int row = 2; row <= rowCount; row++)
            {
                string licensePlate = worksheet.Cells[row, 2].Text.Trim();
                string vehicleTypeName = worksheet.Cells[row, 3].Text.Trim();
                string brand = worksheet.Cells[row, 4].Text.Trim();
                string model = worksheet.Cells[row, 5].Text.Trim();
                string driverName = worksheet.Cells[row, 6].Text.Trim();
                string employeeCode = worksheet.Cells[row, 7].Text.Trim();

                // Skip completely empty rows
                bool isEmptyRow =
                    string.IsNullOrWhiteSpace(licensePlate) &&
                    string.IsNullOrWhiteSpace(vehicleTypeName) &&
                    string.IsNullOrWhiteSpace(brand) &&
                    string.IsNullOrWhiteSpace(model) &&
                    string.IsNullOrWhiteSpace(driverName) &&
                    string.IsNullOrWhiteSpace(employeeCode);

                if (isEmptyRow)
                {
                    continue;
                }

                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(licensePlate))
                {
                    errors.Add("License plate cannot be empty.");
                }

                if (importedPlates.Contains(licensePlate))
                {
                    errors.Add("Duplicate license plate found in file.");
                }

                bool existed = await _context.FleetVehicles.AnyAsync(x => x.LicensePlate == licensePlate);

                if (existed)
                {
                    errors.Add("License plate already exists in the system.");
                }

                importedPlates.Add(licensePlate);

                var vehicleType = await _context.VehicleTypes
                    .FirstOrDefaultAsync(x => x.Name.ToLower().Contains(vehicleTypeName.ToLower()) || 
                                              x.Name == vehicleTypeName);

                if (vehicleType == null)
                {
                    errors.Add($"Vehicle Type '{vehicleTypeName}' not found in the system.");
                }

                if (errors.Any())
                {
                    foreach (var error in errors)
                    {
                        _context.FleetImportErrors.Add(new FleetImportError
                        {
                            FleetImportBatchId = batch.FleetImportBatchId,
                            RowNumber = row,
                            ErrorMessage = error
                        });
                    }

                    batch.FailedRows++;
                    continue;
                }

                // Auto-approve if CarModel (Brand + Model name) already exists and is Active
                bool carModelExists = await _context.CarModels.AnyAsync(x =>
                    x.Brand == brand &&
                    x.Name == model &&
                    x.VehicleTypeId == vehicleType!.Id &&
                    x.IsActive == true);

                var fleetVehicle = new FleetVehicle
                {
                    BusinessProfileId = business.BusinessProfileId,
                    FleetImportBatchId = batch.FleetImportBatchId,
                    LicensePlate = licensePlate,
                    VehicleTypeId = vehicleType!.Id,
                    Brand = brand,
                    Model = model,
                    DriverName = driverName,
                    EmployeeCode = employeeCode,
                    Status = carModelExists ? "Active" : "PendingApproval",
                    CreatedAt = DateTime.UtcNow
                };

                _context.FleetVehicles.Add(fleetVehicle);
                batch.SuccessRows++;
            }
            batch.TotalRows = batch.SuccessRows + batch.FailedRows;

            if (batch.SuccessRows == 0)
            {
                batch.Status = "Failed";
            }
            else if (batch.FailedRows > 0)
            {
                batch.Status = "PartialSuccess";
            }
            else
            {
                batch.Status = "Completed";
            }

            await _context.SaveChangesAsync();

            return new FleetImportResultDTO
            {
                FleetImportBatchId = batch.FleetImportBatchId,
                TotalRows = batch.TotalRows,
                SuccessRows = batch.SuccessRows,
                FailedRows = batch.FailedRows,
                Status = batch.Status
            };
        }

        public async Task<List<FleetImportBatch>> GetImportBatchesAsync()
        {
            return await _context.FleetImportBatches
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<FleetImportDetailDTO> GetImportBatchDetailAsync(int batchId)
        {
            var batch = await _context.FleetImportBatches.FirstOrDefaultAsync(x => x.FleetImportBatchId == batchId);

            if (batch == null)
            {
                throw new NotFoundException("Vehicle import batch not found.");
            }

            var errors = await _context.FleetImportErrors
                    .Where(x => x.FleetImportBatchId == batchId)
                    .Select(x =>
                        new FleetImportErrorDTO
                        {
                            RowNumber = x.RowNumber,
                            ErrorMessage = x.ErrorMessage
                        })
                    .ToListAsync();

            return new FleetImportDetailDTO
            {
                FleetImportBatchId = batch.FleetImportBatchId,
                Status = batch.Status,
                TotalRows = batch.TotalRows,
                SuccessRows = batch.SuccessRows,
                FailedRows = batch.FailedRows,
                Errors = errors
            };
        }

        public async Task<List<FleetVehicleDTO>> GetPendingVehiclesAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles.FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }

            return await _context.FleetVehicles
                .Include(x => x.VehicleType)
                .Where(x =>
                    x.BusinessProfileId ==
                    business.BusinessProfileId &&
                    x.Status == "PendingApproval")
                .Select(x => new FleetVehicleDTO
                {
                    FleetVehicleId = x.FleetVehicleId,
                    LicensePlate = x.LicensePlate,
                    Brand = x.Brand,
                    Model = x.Model,
                    VehicleTypeName = x.VehicleType.Name,
                    DriverName = x.DriverName,
                    EmployeeId = x.EmployeeCode,
                    Status = x.Status
                })
                .ToListAsync();
        }

        public async Task<List<StaffPendingVehicleDTO>> GetAllPendingVehiclesAsync(int? businessProfileId = null)
        {
            var query = _context.FleetVehicles
                .AsQueryable()
                .Include(x => x.VehicleType)
                .Include(x => x.BusinessProfile)
                .Where(x => x.Status == "PendingApproval");

            if (businessProfileId.HasValue)
            {
                query = query.Where(x => x.BusinessProfileId == businessProfileId.Value);
            }

            return await query
                .OrderBy(x => x.CreatedAt)
                .Select(x => new StaffPendingVehicleDTO
                {
                    FleetVehicleId = x.FleetVehicleId,
                    LicensePlate = x.LicensePlate,
                    Brand = x.Brand,
                    Model = x.Model,
                    VehicleTypeName = x.VehicleType.Name,
                    DriverName = x.DriverName,
                    EmployeeId = x.EmployeeCode,
                    Status = x.Status,
                    BusinessName = x.BusinessProfile.CompanyName,
                    BusinessProfileId = x.BusinessProfileId,
                    FleetImportBatchId = x.FleetImportBatchId,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task ApproveFleetVehicleAsync(int fleetVehicleId)
        {
            var vehicle = await _context.FleetVehicles.FirstOrDefaultAsync(x => x.FleetVehicleId == fleetVehicleId);

            if (vehicle == null)
            {
                throw new NotFoundException("Vehicle not found in the fleet.");
            }

            vehicle.Status = "Active";

            await _context.SaveChangesAsync();
        }

        public async Task RejectFleetVehicleAsync(int fleetVehicleId, string reason)
        {
            var vehicle = await _context.FleetVehicles.FirstOrDefaultAsync(x => x.FleetVehicleId == fleetVehicleId);

            if (vehicle == null)
            {
                throw new NotFoundException("Vehicle not found in the fleet.");
            }

            vehicle.Status = "Rejected";
            vehicle.RejectionReason = reason;

            await _context.SaveChangesAsync();
        }

        public async Task<List<FleetQueueDTO>> GetBusinessQueueAsync(int branchId)
        {
            var queue = await _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                .Where(x =>
                    x.BranchId == branchId &&
                    x.Status == "CheckedIn")
                .OrderBy(x => x.CheckInTime)
                .ToListAsync();

            return queue
                .Select((x, index) => new FleetQueueDTO
                {
                    Position = index + 1,
                    FleetWashLogId = x.FleetWashLogId,
                    LicensePlate = x.FleetVehicle.LicensePlate,
                    DriverName = x.FleetVehicle.DriverName,
                    CheckInTime = x.CheckInTime,
                    Status = x.Status!
                })
                .ToList();
        }

        public async Task<List<FleetHistoryDTO>> GetHistoryAsync(int businessUserId, FleetHistoryFilterDTO filter)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }

            var query = _context.FleetWashLogs
                .Include(x => x.FleetVehicle)
                .Include(x => x.Booking)
                .ThenInclude(x => x!.Branch)
                .Where(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId);

            if (filter.FleetVehicleId.HasValue)
            {
                query = query.Where(x => x.FleetVehicleId == filter.FleetVehicleId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x => x.CheckInTime >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x => x.CheckInTime <= filter.ToDate.Value);
            }

            return await query
                .OrderByDescending(x => x.CheckInTime)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new FleetHistoryDTO
                {
                    FleetWashLogId = x.FleetWashLogId,
                    FleetVehicleId = x.FleetVehicleId,
                    LicensePlate = x.FleetVehicle.LicensePlate,
                    DriverName = x.FleetVehicle.DriverName,
                    BranchName = x.Booking != null
                        ? x.Booking.Branch.Name
                        : "Walk-in",
                    CheckInTime = x.CheckInTime,
                    CompletedTime = x.CompletedTime,
                    WashCost = x.WashCost,
                    Status = x.Status!
                })
                .ToListAsync();
        }

        public async Task<FleetDashboardDTO> GetDashboardAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
            {
                throw new NotFoundException("Business profile not found.");
            }

            var today = DateTime.Today;

            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            var totalVehicles =
                await _context.FleetVehicles
                    .CountAsync(x => x.BusinessProfileId == business.BusinessProfileId);

            var activeVehicles =
                await _context.FleetVehicles
                    .CountAsync(x => x.BusinessProfileId == business.BusinessProfileId && x.Status == "Active");

            var pendingVehicles =
                await _context.FleetVehicles
                    .CountAsync(x => x.BusinessProfileId == business.BusinessProfileId && x.Status == "PendingApproval");

            var todayWashCount =
                await _context.FleetWashLogs
                    .CountAsync(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId && x.CheckInTime.Date == today);

            var monthlyWashCount =
                await _context.FleetWashLogs
                    .CountAsync(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId && x.CheckInTime >= firstDayOfMonth);

            var monthlySpend = await _context.FleetWashLogs
                    .Where(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId && x.CheckInTime >= firstDayOfMonth)
                    .SumAsync(x => (decimal?)x.WashCost) ?? 0;

            var vehiclesCurrentlyInStation = await _context.FleetWashLogs
                    .CountAsync(x => x.FleetVehicle.BusinessProfileId == business.BusinessProfileId && (x.Status == "CheckedIn" || x.Status == "Processing"));

            return new FleetDashboardDTO
            {
                TotalVehicles = totalVehicles,
                ActiveVehicles = activeVehicles,
                PendingVehicles = pendingVehicles,
                TodayWashCount = todayWashCount,
                MonthlyWashCount = monthlyWashCount,
                MonthlySpend = monthlySpend,
                VehiclesCurrentlyInStation = vehiclesCurrentlyInStation
            };
        }

        public async Task<List<FleetWashHistoryDTO>> GetWashHistoryAsync(int businessUserId)
        {
            var business = await _context.BusinessProfiles
                .FirstOrDefaultAsync(x => x.UserId == businessUserId);

            if (business == null)
                throw new NotFoundException("Business profile not found.");

            return await _context.FleetWashLogs
                .Include(x => x.Booking)
                .Include(x => x.Booking!.FleetVehicle)
                .Where(x =>
                    x.Booking != null &&
                    x.Booking.BusinessProfileId == business.BusinessProfileId)
                .OrderByDescending(x => x.CheckInTime)
                .Select(x => new FleetWashHistoryDTO
                {
                    FleetWashLogId = x.FleetWashLogId,
                    LicensePlate = x.Booking!.FleetVehicle!.LicensePlate,
                    CheckInTime = x.CheckInTime,
                    CompletedTime = x.CompletedTime,
                    WashCost = x.WashCost,
                    Status = x.Status!
                })
                .ToListAsync();
        }

        public async Task<FleetTemplateDTO> GetFleetTemplateAsync()
        {
            var url = _configuration["FleetTemplate:DownloadUrl"];

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new NotFoundException("Template file download path not configured.");
            }

            return await Task.FromResult(new FleetTemplateDTO
            {
                FileName = "FleetTemplate.xlsx",
                DownloadUrl = url
            });
        }

        public async Task<LaneDTO> CreateBusinessLaneAsync(CreateBusinessLaneDTO dto)
        {
            var branch = await _context.Branches.FindAsync(dto.BranchId);
            if (branch == null) throw new NotFoundException("Branch not found.");

            var lane = new Lane
            {
                Name = dto.Name,
                BranchId = dto.BranchId,
                IsActive = true,
                IsBusinessLane = true
            };

            _context.Lanes.Add(lane);
            await _context.SaveChangesAsync();

            return new LaneDTO
            {
                LaneId = lane.LaneId,
                Name = lane.Name,
                BranchId = lane.BranchId,
                IsActive = lane.IsActive,
                IsBusinessLane = lane.IsBusinessLane
            };
        }
    }
}
