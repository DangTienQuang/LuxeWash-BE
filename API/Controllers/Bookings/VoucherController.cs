using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BLL.Helpers;

namespace AutoWashPro.API.Controllers
{
    [ApiController]
    [Route("api/v1/vouchers")]
    public class VoucherController : ControllerBase
    {
        private readonly IVoucherService _voucherService;

        public VoucherController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyVouchers()
        {
            var userId = ClaimHelper.GetUserId(User);
            var result = await _voucherService.GetMyVouchersAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [Authorize]
        [HttpPost("redeem")]
        public async Task<IActionResult> RedeemVoucher([FromBody] RedeemVoucherRequestDTO request)
        {
            var userId = ClaimHelper.GetUserId(User);
            await _voucherService.RedeemVoucherAsync(userId, request.VoucherId);
            return Ok(new { statusCode = 200, message = "Voucher redeemed successfully." });
        }
    }
}
