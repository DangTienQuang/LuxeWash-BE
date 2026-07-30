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
        public async Task<IActionResult> GetTransactions()
        {
            var result = await _walletService.GetTransactionsAsync(GetCurrentUserId());
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("points/history")]
        public async Task<IActionResult> GetPointsHistory()
        {
            var result = await _walletService.GetPointsHistoryAsync(GetCurrentUserId());
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }
    }
}
