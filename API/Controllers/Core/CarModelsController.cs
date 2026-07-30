using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using System.Threading.Tasks;
using System.Security.Claims;

namespace AutoWashPro.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class CarModelsController : ControllerBase
    {
        private readonly ICarModelService _carModelService;

        public CarModelsController(ICarModelService carModelService)
        {
            _carModelService = carModelService;
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveCarModels()
        {
            var models = await _carModelService.GetActiveCarModelsAsync();
            return Ok(new { statusCode = 200, data = models });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCarModel([FromBody] CreateCarModelDTO request)
        {
            var result = await _carModelService.CreateCarModelAsync(request);
            if (!result)
                return BadRequest(new { statusCode = 400, message = "Car model creation failed!" });

            return Ok(new { statusCode = 201, message = "Car model added successfully!" });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCarModel(int id, [FromBody] UpdateCarModelDTO request)
        {
            var result = await _carModelService.UpdateCarModelAsync(id, request);
            if (!result)
                return NotFound(new { statusCode = 404, message = "Car model not found." });

            return Ok(new { statusCode = 200, message = "Car model updated successfully!" });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCarModel(int id)
        {
            var result = await _carModelService.DeleteCarModelAsync(id);
            if (!result)
                return NotFound(new { statusCode = 404, message = "Car model not found." });

            return Ok(new { statusCode = 200, message = "Car model hidden from the system." });
        }

        [HttpPost("request")]
        [Authorize]
        public async Task<IActionResult> RequestNewCarModel([FromBody] RequestCarModelDTO request)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var newId = await _carModelService.RequestNewCarModelAsync(userId, request);
            return Ok(new { statusCode = 200, message = "Car model request submitted and pending approval.", data = newId });
        }

        [HttpGet("~/api/v1/admin/carmodels/pending")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetPendingCarModels()
        {
            var result = await _carModelService.GetPendingCarModelsAsync();
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPut("~/api/v1/admin/carmodels/{id}/approve")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ApproveCarModel(int id, [FromBody] ApproveCarModelDTO request)
        {
            await _carModelService.ApproveCarModelAsync(id, request);
            return Ok(new { statusCode = 200, message = "Car model approved successfully." });
        }

        [HttpPut("~/api/v1/admin/carmodels/{id}/reject")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> RejectCarModel(int id)
        {
            await _carModelService.RejectCarModelAsync(id);
            return Ok(new { statusCode = 200, message = "Car model rejected." });
        }
    }
}
