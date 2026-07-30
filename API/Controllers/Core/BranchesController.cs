using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoWashPro.API.Controllers
{
    [ApiController]
    [Route("api/v1/branches")]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchService _branchService;

        public BranchesController(IBranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllActiveBranches()
        {
            try
            {
                var branches = await _branchService.GetAllBranchesAsync();
                var activeBranches = branches.Where(b => b.IsActive).ToList();
                return Ok(new { statusCode = 200, message = "Success", data = activeBranches });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }
    }
}
