using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/employees")]
    [Authorize(Roles = "Admin,Manager")]
    public class AdminEmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;

        public AdminEmployeesController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDTO dto)
        {
            var employee = await _employeeService.CreateEmployeeAsync(dto);
            return Ok(employee);
        }

        [HttpPut("{id}/transfer")]
        public async Task<IActionResult> TransferEmployee(int id, [FromBody] TransferEmployeeDTO dto)
        {
            var result = await _employeeService.TransferEmployeeAsync(id, dto);
            return Ok(new { Success = result });
        }
    }
}
