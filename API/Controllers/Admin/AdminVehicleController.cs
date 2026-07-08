using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [Route("api/v1/admin/vehicles")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminVehicleController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;

        public AdminVehicleController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        [HttpGet("other-types")]
        public async Task<IActionResult> GetOtherVehicles()
        {
            var result = await _vehicleService.GetOtherVehiclesAsync();
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("{licensePlate}/type")]
        public async Task<IActionResult> UpdateVehicleType(string licensePlate, [FromBody] UpdateVehicleTypeAdminDTO request)
        {
            await _vehicleService.UpdateVehicleTypeByAdminAsync(licensePlate, request.VehicleTypeId);
            return Ok(new { statusCode = 200, message = "Vehicle type updated successfully." });
        }

        [HttpPost("{licensePlate}/approve-new-type")]
        public async Task<IActionResult> ApproveNewVehicleType(string licensePlate, [FromBody] ApproveVehicleTypeRequestDTO request)
        {
            await _vehicleService.ApproveNewVehicleTypeAsync(licensePlate, request);
            return Ok(new { statusCode = 200, message = "New vehicle type request approved successfully." });
        }

        [HttpPost("{licensePlate}/reject-new-type")]
        public async Task<IActionResult> RejectNewVehicleType(string licensePlate)
        {
            await _vehicleService.RejectNewVehicleTypeAsync(licensePlate);
            return Ok(new { statusCode = 200, message = "New vehicle type request rejected." });
        }
    }
}
