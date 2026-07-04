using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/inventory")]
    [Authorize(Roles = "Admin")]
    public class AdminInventoryController : ControllerBase
    {
        private readonly IMaterialService _materialService;
        private readonly IInventoryTransferService _transferService;
        private readonly IServiceMaterialUsageService _usageService;
        private readonly IInventoryReportService _reportService;

        public AdminInventoryController(
            IMaterialService materialService,
            IInventoryTransferService transferService,
            IServiceMaterialUsageService usageService,
            IInventoryReportService reportService)
        {
            _materialService = materialService;
            _transferService = transferService;
            _usageService = usageService;
            _reportService = reportService;
        }

        [HttpGet("stocks")]
        public async Task<IActionResult> GetStocks([FromQuery] int? branchId)
        {
            return Ok(await _materialService.GetStocksAsync(branchId));
        }

        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches([FromQuery] int? branchId, [FromQuery] bool expiringOnly = false)
        {
            return Ok(await _materialService.GetBatchesAsync(branchId, expiringOnly));
        }

        [HttpGet("condition-multipliers")]
        public async Task<IActionResult> GetConditionMultipliers()
        {
            return Ok(await _usageService.GetConditionMultipliersAsync());
        }

        [HttpPut("condition-multipliers/{id}")]
        public async Task<IActionResult> UpdateConditionMultiplier(int id, [FromBody] UpdateVehicleConditionMaterialMultiplierDTO dto)
        {
            return Ok(await _usageService.UpdateConditionMultiplierAsync(id, dto));
        }

        [HttpGet("reports/profit")]
        public async Task<IActionResult> GetProfitReport([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? branchId)
        {
            return Ok(await _reportService.GetAdminProfitReportAsync(from, to, branchId));
        }

        [HttpGet("branches/{branchId}/settings")]
        public async Task<IActionResult> GetBranchInventorySetting(int branchId)
        {
            return Ok(await _transferService.GetBranchInventorySettingAsync(branchId));
        }

        [HttpPut("branches/{branchId}/settings")]
        public async Task<IActionResult> UpdateBranchInventorySetting(int branchId, [FromBody] UpdateBranchInventorySettingDTO dto)
        {
            return Ok(await _transferService.UpdateBranchInventorySettingAsync(branchId, dto));
        }
    }
}
