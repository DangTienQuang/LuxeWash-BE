using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/services/{serviceId}/materials")]
    [Authorize(Roles = "Admin")]
    public class AdminServiceMaterialUsageController : ControllerBase
    {
        private readonly IServiceMaterialUsageService _usageService;

        public AdminServiceMaterialUsageController(IServiceMaterialUsageService usageService)
        {
            _usageService = usageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetServiceMaterials(int serviceId)
        {
            return Ok(await _usageService.GetByServiceAsync(serviceId));
        }

        [HttpPost]
        public async Task<IActionResult> UpsertServiceMaterials(int serviceId, [FromBody] UpsertServiceMaterialUsageDTO dto)
        {
            return Ok(await _usageService.UpsertAsync(serviceId, dto));
        }

        [HttpPut("{usageId}")]
        public async Task<IActionResult> UpdateServiceMaterial(int serviceId, int usageId, [FromBody] UpsertServiceMaterialUsageDTO dto)
        {
            return Ok(await _usageService.UpdateAsync(serviceId, usageId, dto));
        }
    }
}
