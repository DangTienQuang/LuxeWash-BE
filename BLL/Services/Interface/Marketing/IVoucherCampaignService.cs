using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
namespace AutoWashPro.BLL.Services
{
    public interface IVoucherCampaignService
    {
        Task<CampaignVoucherResponseDTO> CreateBirthdayVouchersAsync(CreateBirthdayVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateAgeVouchersAsync(CreateAgeVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateWinbackVouchersAsync(CreateWinbackVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateVipVouchersAsync(CreateVipVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateMilestoneVouchersAsync(CreateMilestoneVouchersDTO request);
        Task<List<VoucherCampaignProcessResultDTO>> ProcessDailyCampaignsAsync(DateTime? targetDate = null);
        Task<VoucherCampaignProcessResultDTO?> ProcessMilestoneCampaignsAsync(int userId);
    }
}
