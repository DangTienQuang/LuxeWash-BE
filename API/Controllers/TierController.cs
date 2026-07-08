using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/tiers")]
    [ApiController]
    public class TierController : ControllerBase
    {
        private readonly ITierService _tierService;

        public TierController(ITierService tierService)
        {
            _tierService = tierService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTiers()
        {
            var result = await _tierService.GetTiersAsync();
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateTier([FromBody] CreateTierDTO request)
        {
            var result = await _tierService.CreateTierAsync(request);
            return Created("", new { statusCode = 201, message = "Tier created successfully.", data = result });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTier(int id, [FromBody] UpdateTierDTO request)
        {
            var result = await _tierService.UpdateTierAsync(id, request);
            return Ok(new { statusCode = 200, message = "Tier configuration updated successfully.", data = result });
        }
    }
}