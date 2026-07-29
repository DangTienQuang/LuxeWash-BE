using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using AutoWashPro.BLL.Services.Operations;

namespace AutoWashPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager,Staff")]
    public class OperationsMonitoringController : ControllerBase
    {
        private readonly IOperationsMonitoringService _monitoringService;

        public OperationsMonitoringController(IOperationsMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }

        [HttpGet("queue/{branchId}")]
        public async Task<IActionResult> GetQueueMonitoring(int branchId, CancellationToken cancellationToken)
        {
            var data = await _monitoringService.GetQueueMonitoringAsync(branchId, cancellationToken);
            return Ok(data);
        }

        [HttpGet("barrier-commands/failed-expired/{branchId}")]
        public async Task<IActionResult> GetFailedOrExpiredBarrierCommands(int branchId, CancellationToken cancellationToken)
        {
            var data = await _monitoringService.GetFailedOrExpiredBarrierCommandsAsync(branchId, cancellationToken);
            return Ok(data);
        }

        [HttpPost("reconciliation/run/{branchId}")]
        public async Task<IActionResult> RunReconciliationCheck(int branchId, CancellationToken cancellationToken)
        {
            var data = await _monitoringService.RunReconciliationCheckAsync(branchId, cancellationToken);
            return Ok(data);
        }
    }
}
