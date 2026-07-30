using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/services")]
    [ApiController]
    public class ServicesController : ControllerBase
    {
        private readonly IServiceService _serviceService;

        public ServicesController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        [AllowAnonymous] 
        [HttpGet]
        public async Task<IActionResult> GetActiveServices([FromQuery] int? branchId)
        {
            try
            {
                var result = await _serviceService.GetActiveServicesAsync(branchId);
                return Ok(new { statusCode = 200, message = "Success", data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetServiceById(int id)
        {
            try
            {
                var result = await _serviceService.GetServiceByIdAsync(id);
                return Ok(new { statusCode = 200, message = "Success", data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }
    }
}