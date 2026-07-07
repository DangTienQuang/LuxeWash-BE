using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/material-units")]
    [Authorize(Roles = "Admin")]
    public class AdminMaterialUnitsController : ControllerBase
    {
        private readonly IMaterialService _materialService;

        public AdminMaterialUnitsController(IMaterialService materialService)
        {
            _materialService = materialService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterialUnits([FromQuery] bool includeInactive = false)
        {
            return Ok(await _materialService.GetMaterialUnitsAsync(includeInactive));
        }

        [HttpPost]
        public async Task<IActionResult> CreateMaterialUnit([FromBody] CreateMaterialUnitDTO dto)
        {
            var result = await _materialService.CreateMaterialUnitAsync(dto);
            return CreatedAtAction(nameof(GetMaterialUnits), new { id = result.UnitId }, result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMaterialUnit(int id, [FromBody] UpdateMaterialUnitDTO dto)
        {
            return Ok(await _materialService.UpdateMaterialUnitAsync(id, dto));
        }
    }
}
