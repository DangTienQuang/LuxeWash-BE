using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace API.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/branches")]
    [Authorize(Roles = "Admin")]
    public class AdminBranchesController : ControllerBase
    {
        private readonly IBranchService _branchService;

        public AdminBranchesController(IBranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBranches()
        {
            var branches = await _branchService.GetAllBranchesAsync();
            return Ok(branches);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBranch(int id)
        {
            var branch = await _branchService.GetBranchByIdAsync(id);
            return Ok(branch);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBranch([FromBody] CreateBranchDTO dto)
        {
            var branch = await _branchService.CreateBranchAsync(dto);
            return CreatedAtAction(nameof(GetBranch), new { id = branch.BranchId }, branch);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(int id, [FromBody] UpdateBranchDTO dto)
        {
            var branch = await _branchService.UpdateBranchAsync(id, dto);
            return Ok(branch);
        }

        [HttpGet("{id}/employees")]
        public async Task<IActionResult> GetBranchEmployees(int id)
        {
            var summary = await _branchService.GetBranchEmployeeSummaryAsync(id);
            return Ok(summary);
        }

        [HttpGet("employees-summary")]
        public async Task<IActionResult> GetAllBranchesEmployeesSummary()
        {
            var summary = await _branchService.GetAllBranchEmployeesSummaryAsync();
            return Ok(summary);
        }
    }
}
