using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.Staff
{
    [ApiController]
    [Route("api/v1/staff/vouchers")]
    [Authorize(Roles = "Staff,Manager,Admin")]
    public class StaffVouchersController : ControllerBase
    {
        private readonly IVoucherService _voucherService;

        public StaffVouchersController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        [HttpPost("consume")]
        public async Task<IActionResult> ConsumePhysicalVoucher([FromBody] ConsumeVoucherRequestDTO request)
        {
            var result = await _voucherService.ConsumePhysicalVoucherAsync(request.UserId, request.VoucherCode);
            return Ok(new { statusCode = 200, message = "Physical voucher redeemed successfully." });
        }
    }
}
