using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Admin
{
    [Route("api/v1/admin/vouchers")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminVouchersController : ControllerBase
    {
        private readonly IVoucherService _voucherService;
        private readonly IVoucherCampaignService _voucherCampaignService;
        private readonly ICRMCampaignService _crmCampaignService;

        public AdminVouchersController(
            IVoucherService voucherService,
            IVoucherCampaignService voucherCampaignService,
            ICRMCampaignService crmCampaignService)
        {
            _voucherService = voucherService;
            _voucherCampaignService = voucherCampaignService;
            _crmCampaignService = crmCampaignService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _voucherService.GetAllVouchersAsync();
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrUpdateVoucherDTO request)
        {
            var result = await _voucherService.CreateVoucherAsync(request);
            return Created("", new { statusCode = 201, message = "Voucher created successfully.", data = result });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOrUpdateVoucherDTO request)
        {
            var result = await _voucherService.UpdateVoucherAsync(id, request);
            return Ok(new { statusCode = 200, message = "Voucher updated successfully.", data = result });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _voucherService.DeleteVoucherAsync(id);
            return Ok(new { statusCode = 200, message = "Voucher deleted successfully." });
        }

        [HttpPost("{id}/grant")]
        public async Task<IActionResult> GrantVouchers(int id, [FromBody] GrantVoucherRequestDTO request)
        {
            await _voucherService.GrantVouchersAsync(id, request.UserIds);
            return Ok(new { statusCode = 200, message = "Vouchers granted successfully." });
        }

        [HttpPost("birthday")]
        public async Task<IActionResult> CreateBirthdayVouchers([FromBody] CreateBirthdayVouchersDTO request)
        {
            var result = await _voucherCampaignService.CreateBirthdayVouchersAsync(request);
            return Created("", new { statusCode = 201, message = "Birthday voucher rule created successfully.", data = result });
        }

        [HttpPost("age")]
        public async Task<IActionResult> CreateAgeVouchers([FromBody] CreateAgeVouchersDTO request)
        {
            var result = await _voucherCampaignService.CreateAgeVouchersAsync(request);
            return Created("", new { statusCode = 201, message = "Age-based voucher rule created successfully.", data = result });
        }

        [HttpPost("winback")]
        public async Task<IActionResult> CreateWinbackVouchers([FromBody] CreateWinbackVouchersDTO request)
        {
            var result = await _voucherCampaignService.CreateWinbackVouchersAsync(request);
            return Created("", new { statusCode = 201, message = "Winback voucher rule created successfully.", data = result });
        }

        [HttpPost("vip")]
        public async Task<IActionResult> CreateVipVouchers([FromBody] CreateVipVouchersDTO request)
        {
            var result = await _voucherCampaignService.CreateVipVouchersAsync(request);
            return Created("", new { statusCode = 201, message = "VIP voucher rule created successfully.", data = result });
        }

        [HttpPost("milestone")]
        public async Task<IActionResult> CreateMilestoneVouchers([FromBody] CreateMilestoneVouchersDTO request)
        {
            var result = await _voucherCampaignService.CreateMilestoneVouchersAsync(request);
            return Created("", new { statusCode = 201, message = "Usage milestone voucher rule created successfully.", data = result });
        }
        [HttpPost("process-campaigns")]
        public async Task<IActionResult> ProcessCampaignsNow()
        {
            var result = await _voucherCampaignService.ProcessDailyCampaignsAsync();
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("trigger-weather")]
        public async Task<IActionResult> TriggerWeatherCampaign()
        {
            var result = await _crmCampaignService.TriggerWeatherCampaignAsync();
            return Ok(new { statusCode = 200, message = result });
        }

        [HttpPost("simulate-weather")]
        public async Task<IActionResult> SimulateWeatherCampaign([FromBody] WeatherCampaignSimulationRequestDTO request)
        {
            var result = await _crmCampaignService.SimulateSmartWeatherCampaignAsync(request);
            return Ok(new { statusCode = 200, message = result });
        }
    }
}
