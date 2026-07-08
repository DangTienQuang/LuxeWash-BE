using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/vehicles")]
    [ApiController]
    [Authorize]
    public class VehicleController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;

        public VehicleController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyVehicles()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var result = await _vehicleService.GetMyVehiclesAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost]
        public async Task<IActionResult> AddVehicle([FromForm] CreateVehicleDTO request)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            await _vehicleService.AddVehicleAsync(userId, request);
            return Created("", new { statusCode = 201, message = "Vehicle added successfully." });
        }

        [HttpPut("{licensePlate}")]
        public async Task<IActionResult> UpdateVehicle(string licensePlate, [FromForm] UpdateVehicleDTO request)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            await _vehicleService.UpdateVehicleAsync(userId, licensePlate, request);
            return Ok(new { statusCode = 200, message = "Vehicle information updated successfully." });
        }

        [HttpDelete("{licensePlate}")]
        public async Task<IActionResult> DeleteVehicle(string licensePlate)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            await _vehicleService.DeleteVehicleAsync(userId, licensePlate);
            return Ok(new { statusCode = 200, message = "Vehicle removed from profile successfully." });
        }

        [Authorize(Roles = "Admin")] 
        [HttpGet("recognize/{licensePlate}")]
        public async Task<IActionResult> RecognizeVehicle(string licensePlate)
        {
            var result = await _vehicleService.RecognizeVehicleAsync(licensePlate);
            return Ok(new { statusCode = 200, message = "Recognition successful", data = result });
        }
    }
}