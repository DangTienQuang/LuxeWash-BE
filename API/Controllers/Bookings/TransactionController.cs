using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BLL.Helpers;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1")]
    [ApiController]
    [Authorize]
    public class TransactionController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public TransactionController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        private int GetCurrentUserId()
        {
            return ClaimHelper.GetUserId(User);
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _walletService.GetTransactionsAsync(GetCurrentUserId(), page, pageSize);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("points/history")]
        public async Task<IActionResult> GetPointsHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var result = await _walletService.GetPointsHistoryAsync(GetCurrentUserId(), page, pageSize);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }
    }
}
