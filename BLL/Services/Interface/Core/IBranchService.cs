using AutoWashPro.BLL.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface IBranchService
    {
        Task<List<BranchDTO>> GetAllBranchesAsync();
        Task<BranchDTO> GetBranchByIdAsync(int branchId);
        Task<BranchDTO> CreateBranchAsync(CreateBranchDTO createDto);
        Task<BranchDTO> UpdateBranchAsync(int branchId, UpdateBranchDTO updateDto);
        Task<BranchEmployeeSummaryDTO> GetBranchEmployeeSummaryAsync(int branchId);
    }
}
