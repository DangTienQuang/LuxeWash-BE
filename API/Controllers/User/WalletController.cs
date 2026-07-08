using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BLL.Helpers;

namespace API.Controllers.User
{
    [ApiController]
    [Route("api/v1/wallets")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyWallet()
        {
            var userId = ClaimHelper.GetUserId(User);
            var result = await _walletService.GetWalletInfoAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [Authorize]
        [HttpPost("top-up")]
        public async Task<IActionResult> TopUp([FromBody] TopUpRequestDTO request)
        {
            var userId = ClaimHelper.GetUserId(User);
            var result = await _walletService.CreateTopUpLinkAsync(userId, request);
            return Ok(new { statusCode = 200, message = "Top-up QR code created successfully.", data = result });
        }

        [HttpPost("top-up/callback")]
        public async Task<IActionResult> PayOsTopUpCallback([FromBody] WebhookTopUpDTO webhookData)
        {
            await _walletService.ProcessPayOsWebhookAsync(webhookData);
            return Ok(new { statusCode = 200, message = "Success" });
        }
    }
}
