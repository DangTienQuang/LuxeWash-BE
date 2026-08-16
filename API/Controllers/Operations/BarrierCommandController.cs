using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoWashPro.BLL.Services.Operations;
using BLL.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace API.Controllers.Operations
{
    [Route("api/v1/barrier")]
    [ApiController]
    public class BarrierCommandController : ControllerBase
    {
        private const string DeviceStatusCachePrefix = "barrier-device-status:";
        private readonly IOperationsMonitoringService _monitoringService;
        private readonly BarrierDeviceOptions _deviceOptions;
        private readonly IMemoryCache _cache;

        public BarrierCommandController(
            IOperationsMonitoringService monitoringService,
            IOptions<BarrierDeviceOptions> deviceOptions,
            IMemoryCache cache)
        {
            _monitoringService = monitoringService;
            _deviceOptions = deviceOptions.Value;
            _cache = cache;
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("commands")]
        public async Task<IActionResult> CreateManualCommand(
            [FromBody] CreateBarrierCommandRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var command = await _monitoringService.CreateManualBarrierCommandAsync(
                    ClaimHelper.GetUserId(User),
                    request.BarrierId,
                    request.Action,
                    cancellationToken);
                return Accepted(command);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpGet("commands/{commandId}")]
        public async Task<IActionResult> GetCommand(string commandId, CancellationToken cancellationToken)
        {
            var branchId = await _monitoringService.GetEmployeeBranchIdAsync(
                ClaimHelper.GetUserId(User), cancellationToken);
            if (branchId == null) return BadRequest(new { message = "Employee is not assigned to a branch." });

            var command = await _monitoringService.GetBarrierCommandAsync(
                branchId.Value, commandId, cancellationToken);
            return command == null ? NotFound(new { message = "Command not found." }) : Ok(command);
        }

        [Authorize(Roles = "Staff,Manager")]
        [HttpGet("device/status")]
        public async Task<IActionResult> GetDeviceStatus(CancellationToken cancellationToken)
        {
            var branchId = await _monitoringService.GetEmployeeBranchIdAsync(
                ClaimHelper.GetUserId(User), cancellationToken);
            if (branchId == null) return BadRequest(new { message = "Employee is not assigned to a branch." });

            var cacheKey = DeviceStatusCachePrefix + branchId.Value;
            if (!_cache.TryGetValue(cacheKey, out BarrierDeviceHeartbeatSnapshot? snapshot) || snapshot == null)
            {
                return Ok(new
                {
                    online = false,
                    deviceId = _deviceOptions.DeviceId,
                    lastSeenAt = (DateTime?)null,
                    gates = new { }
                });
            }

            var online = DateTime.UtcNow - snapshot.LastSeenAt
                <= TimeSpan.FromSeconds(Math.Max(5, _deviceOptions.OfflineAfterSeconds));
            return Ok(new
            {
                online,
                snapshot.DeviceId,
                snapshot.LastSeenAt,
                snapshot.IpAddress,
                snapshot.WifiRssi,
                snapshot.UptimeMs,
                snapshot.Gates
            });
        }

        [AllowAnonymous]
        [HttpGet("device/commands/next")]
        public async Task<IActionResult> GetNextDeviceCommand(CancellationToken cancellationToken)
        {
            if (!TryAuthorizeDevice(out var error)) return error!;
            var command = await _monitoringService.GetNextBarrierCommandAsync(
                _deviceOptions.BranchId, cancellationToken);
            return command == null ? NoContent() : Ok(command);
        }

        [AllowAnonymous]
        [HttpPost("device/commands/{commandId}/ack")]
        public async Task<IActionResult> AckDeviceCommand(
            string commandId,
            [FromBody] BarrierAckRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryAuthorizeDevice(out var error)) return error!;
            var found = await _monitoringService.AckBarrierCommandAsync(
                _deviceOptions.BranchId, commandId, request.Status, cancellationToken);
            return found
                ? Ok(new { message = "ACK processed successfully." })
                : NotFound(new { message = "Command not found." });
        }

        [AllowAnonymous]
        [HttpPost("device/heartbeat")]
        public IActionResult Heartbeat([FromBody] BarrierHeartbeatRequest request)
        {
            if (!TryAuthorizeDevice(out var error)) return error!;
            var snapshot = new BarrierDeviceHeartbeatSnapshot
            {
                DeviceId = _deviceOptions.DeviceId,
                LastSeenAt = DateTime.UtcNow,
                IpAddress = request.IpAddress,
                WifiRssi = request.WifiRssi,
                UptimeMs = request.UptimeMs,
                Gates = request.Gates.Clone()
            };
            _cache.Set(
                DeviceStatusCachePrefix + _deviceOptions.BranchId,
                snapshot,
                TimeSpan.FromDays(1));
            return Ok(new { serverTime = snapshot.LastSeenAt });
        }

        // Kept for compatibility with older staff builds during rollout.
        [Authorize(Roles = "Staff,Manager")]
        [HttpPost("ack")]
        public async Task<IActionResult> AckBarrierCommand([FromBody] BarrierAckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CommandId))
                return BadRequest(new { message = "CommandId is required." });

            var found = await _monitoringService.AckBarrierCommandAsync(request.CommandId, request.Status);
            return found
                ? Ok(new { message = "ACK processed successfully." })
                : NotFound(new { message = "Command not found." });
        }

        private bool TryAuthorizeDevice(out IActionResult? error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(_deviceOptions.DeviceKey)
                || string.IsNullOrWhiteSpace(_deviceOptions.DeviceId)
                || _deviceOptions.BranchId <= 0)
            {
                error = StatusCode(503, new { message = "Barrier device is not configured." });
                return false;
            }

            var deviceId = Request.Headers["X-Device-Id"].ToString();
            var deviceKey = Request.Headers["X-Device-Key"].ToString();
            if (!FixedTimeEquals(deviceId, _deviceOptions.DeviceId)
                || !FixedTimeEquals(deviceKey, _deviceOptions.DeviceKey))
            {
                error = Unauthorized(new { message = "Invalid barrier device credentials." });
                return false;
            }
            return true;
        }

        private static bool FixedTimeEquals(string supplied, string expected)
        {
            var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
            var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
            return suppliedBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
        }
    }

    public sealed class BarrierDeviceOptions
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceKey { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int OfflineAfterSeconds { get; set; } = 20;
    }

    public sealed class CreateBarrierCommandRequest
    {
        public string BarrierId { get; set; } = string.Empty;
        public string Action { get; set; } = "OPEN";
    }

    public sealed class BarrierAckRequest
    {
        public string CommandId { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? Details { get; set; }
    }

    public sealed class BarrierHeartbeatRequest
    {
        public string? IpAddress { get; set; }
        public int? WifiRssi { get; set; }
        public long UptimeMs { get; set; }
        public JsonElement Gates { get; set; }
    }

    internal sealed class BarrierDeviceHeartbeatSnapshot
    {
        public string DeviceId { get; set; } = string.Empty;
        public DateTime LastSeenAt { get; set; }
        public string? IpAddress { get; set; }
        public int? WifiRssi { get; set; }
        public long UptimeMs { get; set; }
        public JsonElement Gates { get; set; }
    }
}
