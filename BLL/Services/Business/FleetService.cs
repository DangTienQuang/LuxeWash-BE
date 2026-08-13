#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.DTOs.Fleet;
using BLL.Services.Interface;
using DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        public FleetService(AutoWashDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }
        public async Task<FleetImportResultDTO> ImportFleetAsync(int userId, IFormFile file)
        {
            var business = await _context.BusinessProfiles.FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.ApprovalStatus == "Approved");
            if (business == null)
            {
                throw new BadRequestException("Tài khoản doanh nghiệp chưa được phê duyệt.");
            }
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("Vui lòng chọn file Excel có dữ liệu để nhập.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("Định dạng file không hợp lệ. Vui lòng sử dụng file Excel .xlsx tải từ hệ thống.");
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            ExcelPackage package;
            try
            {
                package = new ExcelPackage(stream);
            }
            catch
            {
                throw new BadRequestException("Không thể đọc file Excel. File có thể bị hỏng hoặc không đúng định dạng .xlsx.");
            }

            using var packageScope = package;
            var worksheet = package.Workbook.Worksheets[0];
            if (worksheet?.Dimension == null)
            {
                throw new BadRequestException("File Excel không có dữ liệu. Vui lòng thêm ít nhất một xe từ dòng 2.");
            }

            var expectedHeaders = new[]
            {
                "STT",
                "Biển số xe (*)",
                "Loại xe (*)",
                "Hãng xe",
                "Mẫu xe",
                "Tên tài xế",
                "Mã nhân viên"
            };
            for (var column = 1; column <= expectedHeaders.Length; column++)
            {
                var actualHeader = worksheet.Cells[1, column].Text.Trim();
                if (!string.Equals(actualHeader, expectedHeaders[column - 1], StringComparison.OrdinalIgnoreCase))
                {
                    throw new BadRequestException(
                        $"Cấu trúc file không đúng: cột {column} phải là '{expectedHeaders[column - 1]}'. Vui lòng tải lại file mẫu mới nhất.");
                }
            }

            int rowCount = worksheet.Dimension.Rows;
            var dataRows = Enumerable.Range(2, Math.Max(0, rowCount - 1))
                .Where(row => Enumerable.Range(2, 6)
                    .Any(column => !string.IsNullOrWhiteSpace(worksheet.Cells[row, column].Text)))
                .ToList();
            if (!dataRows.Any())
            {
                throw new BadRequestException(
                    "File Excel chưa có dòng xe nào. Hãy nhập dữ liệu từ dòng 2, lưu file, đóng Excel rồi chọn lại file để nhập.");
            }

            var fileUrl = await _cloudinaryService.UploadFileAsync(file, "fleet-imports");
            var batch = new FleetImportBatch
            {
                BusinessProfileId = business.BusinessProfileId,
                FileUrl = fileUrl,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };
            _context.FleetImportBatches.Add(batch);
            await _context.SaveChangesAsync();

            var requestedLicensePlates = dataRows
                .Select(r => worksheet.Cells[r, 2].Text.Trim().Replace("-", "").Replace(".", "").Replace(" ", "").ToUpperInvariant())
                .Where(lp => !string.IsNullOrWhiteSpace(lp))
                .Distinct()
                .ToList();

            var existingLicensePlatesInDb = await _context.FleetVehicles
                .Where(v => requestedLicensePlates.Contains(v.LicensePlate))
                .Select(v => v.LicensePlate)
                .ToListAsync();
            var existingLicensePlatesSet = new HashSet<string>(existingLicensePlatesInDb, StringComparer.OrdinalIgnoreCase);

            var allVehicleTypes = await _context.VehicleTypes.ToListAsync();

            var requestedBrands = dataRows.Select(r => worksheet.Cells[r, 4].Text.Trim()).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();
            var requestedModels = dataRows.Select(r => worksheet.Cells[r, 5].Text.Trim()).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();

            var activeCarModels = await _context.CarModels
                .Where(c => c.IsActive == true && c.Status == "Approved" && requestedBrands.Contains(c.Brand) && requestedModels.Contains(c.Name))
                .ToListAsync();

            var importedPlates = new HashSet<string>();
            var importErrors = new List<FleetImportErrorDTO>();
            var importedVehicles = new List<FleetImportVehicleResultDTO>();
            
            foreach (var row in dataRows)
            {
                string licensePlate = worksheet.Cells[row, 2].Text
                    .Trim()
                    .Replace("-", "")
                    .Replace(".", "")
                    .Replace(" ", "")
                    .ToUpperInvariant();
                string vehicleTypeName = worksheet.Cells[row, 3].Text.Trim();
                string brand = worksheet.Cells[row, 4].Text.Trim();
                string model = worksheet.Cells[row, 5].Text.Trim();
                string driverName = worksheet.Cells[row, 6].Text.Trim();
                string employeeCode = worksheet.Cells[row, 7].Text.Trim();

                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(licensePlate))
                {
                    errors.Add("Biển số xe không được để trống.");
                }
                else if (!importedPlates.Add(licensePlate))
                {
                    errors.Add($"Biển số '{licensePlate}' bị trùng trong file.");
                }

                if (!string.IsNullOrWhiteSpace(licensePlate))
                {
                    if (existingLicensePlatesSet.Contains(licensePlate))
                    {
                        errors.Add($"Biển số '{licensePlate}' đã tồn tại trong hệ thống.");
                    }
                }

                VehicleType? vehicleType = null;
                if (string.IsNullOrWhiteSpace(vehicleTypeName))
                {
                    errors.Add("Loại xe không được để trống.");
                }
                else
                {
                    vehicleType = allVehicleTypes.FirstOrDefault(x => 
                        string.Equals(x.Name, vehicleTypeName, StringComparison.OrdinalIgnoreCase) || 
                        x.Name.Contains(vehicleTypeName, StringComparison.OrdinalIgnoreCase));
                        
                    if (vehicleType == null)
                    {
                        errors.Add($"Loại xe '{vehicleTypeName}' không tồn tại trong hệ thống. Vui lòng nhập đúng tên loại xe.");
                    }
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
                        importErrors.Add(new FleetImportErrorDTO
                        {
                            RowNumber = row,
                            ErrorMessage = error
                        });
                    }
                    batch.FailedRows++;
                    continue;
                }
                
                bool carModelExists = activeCarModels.Any(x =>
                    string.Equals(x.Brand, brand, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Name, model, StringComparison.OrdinalIgnoreCase) &&
                    x.VehicleTypeId == vehicleType!.Id);

                var vehicleStatus = carModelExists ? "Active" : "PendingApproval";
                
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
                    Status = vehicleStatus,
                    CreatedAt = DateTime.UtcNow
                };
                _context.FleetVehicles.Add(fleetVehicle);
                
                importedVehicles.Add(new FleetImportVehicleResultDTO
                {
                    RowNumber = row,
                    LicensePlate = licensePlate,
                    Status = vehicleStatus,
                    Message = vehicleStatus == "Active"
                        ? "Đã được duyệt tự động và có thể sử dụng."
                        : "Hãng hoặc mẫu xe chưa được duyệt trong hệ thống. Xe đang chờ Admin duyệt."
                });
                
                batch.SuccessRows++;
            }
            batch.TotalRows = dataRows.Count;
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
                ApprovedRows = importedVehicles.Count(x => x.Status == "Active"),
                PendingApprovalRows = importedVehicles.Count(x => x.Status == "PendingApproval"),
                Status = batch.Status,
                Errors = importErrors,
                Vehicles = importedVehicles
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
        public Task<FleetTemplateDTO> GetFleetTemplateInfoAsync(string fallbackBaseUrl)
        {
            var baseUrl = fallbackBaseUrl.TrimEnd('/');
            return Task.FromResult(new FleetTemplateDTO
            {
                FileName = "FleetTemplate.xlsx",
                DownloadUrl = $"{baseUrl}/api/v1/fleet/template/download?v=3"
            });
        }
        public async Task<byte[]> GenerateFleetTemplateAsync()
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Fleet Template");
            worksheet.Cells[1, 1].Value = "STT";
            worksheet.Cells[1, 2].Value = "Biển số xe (*)";
            worksheet.Cells[1, 3].Value = "Loại xe (*)";
            worksheet.Cells[1, 4].Value = "Hãng xe";
            worksheet.Cells[1, 5].Value = "Mẫu xe";
            worksheet.Cells[1, 6].Value = "Tên tài xế";
            worksheet.Cells[1, 7].Value = "Mã nhân viên";

            var sampleVehicles = new object[,]
            {
                { 1, "51A12345", "Sedan", "Toyota", "Vios", "Nguyễn Văn An", "NV001" },
                { 2, "51B67890", "Sedan", "Honda", "City", "Trần Minh Bình", "NV002" },
                { 3, "51C24680", "Sedan", "Hyundai", "Accent", "Lê Hoàng Nam", "NV003" }
            };
            worksheet.Cells[2, 1, 4, 7].Value = sampleVehicles;

            using (var range = worksheet.Cells[1, 1, 1, 7])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

            using (var sampleRange = worksheet.Cells[2, 1, 4, 7])
            {
                sampleRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                sampleRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                sampleRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Hair;
                sampleRange.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.LightGray);
                sampleRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Hair;
                sampleRange.Style.Border.Right.Color.SetColor(System.Drawing.Color.LightGray);
            }

            worksheet.Column(2).Style.Numberformat.Format = "@";
            worksheet.Column(7).Style.Numberformat.Format = "@";
            worksheet.Cells[2, 1, 4, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            worksheet.Cells[2, 3, 4, 3].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            worksheet.Cells[2, 1].AddComment(
                "Đây là dữ liệu mẫu. Hãy thay bằng thông tin xe thật trước khi nhập.",
                "LuxeWash Pro");
            worksheet.View.FreezePanes(2, 1);
            worksheet.Cells[1, 1, 4, 7].AutoFilter = true;
            worksheet.Cells.AutoFitColumns();
            return await package.GetAsByteArrayAsync();
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
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
