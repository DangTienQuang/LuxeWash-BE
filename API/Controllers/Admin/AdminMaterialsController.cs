using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/materials")]
    [Authorize(Roles = "Admin")]
    public class AdminMaterialsController : ControllerBase
    {
        private readonly IMaterialService _materialService;

        public AdminMaterialsController(IMaterialService materialService)
        {
            _materialService = materialService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterials([FromQuery] bool includeInactive = false)
        {
            return Ok(await _materialService.GetMaterialsAsync(includeInactive));
        }

        [HttpPost]
        public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialDTO dto)
        {
            var result = await _materialService.CreateMaterialAsync(dto);
            return CreatedAtAction(nameof(GetMaterials), new { id = result.MaterialId }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMaterial(int id, [FromBody] UpdateMaterialDTO dto)
        {
            return Ok(await _materialService.UpdateMaterialAsync(id, dto));
        }
    }
}
