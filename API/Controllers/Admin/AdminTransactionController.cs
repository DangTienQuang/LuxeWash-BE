using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/transactions")]
    [Authorize(Roles = "Admin")]
    public class AdminTransactionController : ControllerBase
    {
        private readonly IWalletService _walletService;
        public AdminTransactionController(IWalletService walletService) => _walletService = walletService;

        // To is exclusive, allowing an inclusive date filter without losing subsecond transactions.
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] AdminTransactionQueryDTO request)
        {
            if (request.From.HasValue && request.To.HasValue && request.From >= request.To)
                return BadRequest(new { statusCode = 400, message = "Khoảng thời gian không hợp lệ." });
            var result = await _walletService.GetAdminTransactionsAsync(request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }
    }
}
