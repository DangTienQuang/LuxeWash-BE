using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Manager
{
    [ApiController]
    [Route("api/v1/manager/inventory")]
    [Authorize(Roles = "Manager")]
    public class ManagerInventoryController : ControllerBase
    {
        private readonly IInventoryTransferService _transferService;
        private readonly IInventoryReportService _reportService;
        private readonly IBookingMaterialUsageService _bookingMaterialUsageService;

        public ManagerInventoryController(
            IInventoryTransferService transferService,
            IInventoryReportService reportService,
            IBookingMaterialUsageService bookingMaterialUsageService)
        {
            _transferService = transferService;
            _reportService = reportService;
            _bookingMaterialUsageService = bookingMaterialUsageService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("stocks")]
        public async Task<IActionResult> GetMyStocks()
        {
            return Ok(await _transferService.GetManagerStocksAsync(GetUserId()));
        }

        [HttpPost("imports")]
        public async Task<IActionResult> ImportBatchToMyBranch([FromBody] ImportMaterialBatchDTO dto)
        {
            return Ok(await _transferService.ImportBatchToManagerBranchAsync(GetUserId(), dto));
        }

        [HttpGet("batches")]
        public async Task<IActionResult> GetMyBatches([FromQuery] int? materialId, [FromQuery] bool includeDepleted = false)
        {
            return Ok(await _transferService.GetManagerBatchesAsync(GetUserId(), materialId, includeDepleted));
        }

        [HttpPost("batches/{id}/discard")]
        public async Task<IActionResult> DiscardMyBatch(int id, [FromBody] DiscardMaterialBatchDTO dto)
        {
            await _transferService.DiscardManagerBatchAsync(GetUserId(), id, dto?.Reason);
            return Ok(new { Message = "Batch discarded successfully." });
        }

        [HttpPost("adjustments")]
        public async Task<IActionResult> AdjustMyStock([FromBody] AdjustBranchInventoryDTO dto)
        {
            return Ok(await _transferService.AdjustManagerStockAsync(GetUserId(), dto));
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetMyTransactions([FromQuery] int? materialId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? type)
        {
            return Ok(await _transferService.GetManagerTransactionsAsync(GetUserId(), materialId, from, to, type));
        }

        [HttpGet("expiring-soon")]
        public async Task<IActionResult> GetExpiringBatches()
        {
            return Ok(await _transferService.GetManagerExpiringBatchesAsync(GetUserId()));
        }

        [HttpGet("reports/profit")]
        public async Task<IActionResult> GetProfitReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            return Ok(await _reportService.GetManagerProfitReportAsync(GetUserId(), from, to));
        }

        [HttpGet("extra-usage-requests")]
        public async Task<IActionResult> GetExtraUsageRequests([FromQuery] string? status)
        {
            return Ok(await _bookingMaterialUsageService.GetManagerExtraUsageRequestsAsync(GetUserId(), status));
        }

        [HttpPost("extra-usage-requests/{id}/approve")]
        public async Task<IActionResult> ApproveExtraUsageRequest(int id, [FromBody] ReviewExtraMaterialUsageRequestDTO dto)
        {
            return Ok(await _bookingMaterialUsageService.ApproveExtraUsageRequestAsync(GetUserId(), id, dto));
        }

        [HttpPost("extra-usage-requests/{id}/reject")]
        public async Task<IActionResult> RejectExtraUsageRequest(int id, [FromBody] ReviewExtraMaterialUsageRequestDTO dto)
        {
            return Ok(await _bookingMaterialUsageService.RejectExtraUsageRequestAsync(GetUserId(), id, dto));
        }
    }
}
