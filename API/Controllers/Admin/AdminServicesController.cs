using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [Route("api/v1/admin/services")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminServicesController : ControllerBase
    {
        private readonly IServiceService _serviceService;

        public AdminServicesController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllServices([FromQuery] int? branchId)
        {
            try
            {
                var result = await _serviceService.GetAllServicesAsync(branchId);
                return Ok(new { statusCode = 200, message = "Success", data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateService([FromBody] CreateOrUpdateServiceDTO request)
        {
            try
            {
                var result = await _serviceService.CreateServiceAsync(request);
                return Created("", new { statusCode = 201, message = "Service created successfully", data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateService(int id, [FromBody] CreateOrUpdateServiceDTO request)
        {
            try
            {
                await _serviceService.UpdateServiceAsync(id, request);
                return Ok(new { statusCode = 200, message = "Service updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> ToggleServiceStatus(int id)
        {
            try
            {
                await _serviceService.DeleteServiceAsync(id);
                return Ok(new { statusCode = 200, message = "Service status changed successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }
    }
}