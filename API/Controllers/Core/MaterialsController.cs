using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/v1/materials")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class MaterialsController : ControllerBase
    {
        private readonly IMaterialService _materialService;

        public MaterialsController(IMaterialService materialService)
        {
            _materialService = materialService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterials([FromQuery] bool includeInactive = false)
        {
            var canIncludeInactive = includeInactive && User.IsInRole("Admin");
            return Ok(await _materialService.GetMaterialsAsync(canIncludeInactive));
        }

        [HttpGet("units")]
        public async Task<IActionResult> GetMaterialUnits()
        {
            return Ok(await _materialService.GetMaterialUnitsAsync());
        }
    }
}
