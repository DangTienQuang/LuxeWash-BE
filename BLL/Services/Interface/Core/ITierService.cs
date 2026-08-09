using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
namespace AutoWashPro.BLL.Services
{
    public interface ITierService
    {
        Task<List<TierResponseDTO>> GetTiersAsync();
        Task<TierResponseDTO> CreateTierAsync(CreateTierDTO request);
        Task<TierResponseDTO> UpdateTierAsync(int id, UpdateTierDTO request);
        Task<TierUpgradeResultDTO?> EvaluateAndUpgradeTierAsync(int userId);
        Task<TierUpgradeResultDTO?> EvaluateTierForProfileAsync(int userId);
    }
}