using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Enums;
namespace AutoWashPro.BLL.Services
{
    public interface IVoucherCampaignService
    {
        Task<CampaignVoucherResponseDTO> CreateBirthdayVouchersAsync(CreateBirthdayVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateWinbackVouchersAsync(CreateWinbackVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateVipVouchersAsync(CreateVipVouchersDTO request);
        Task<CampaignVoucherResponseDTO> CreateWelcomeVouchersAsync(CreateWelcomeVouchersDTO request);
        
        Task<List<VoucherCampaignProcessResultDTO>> ProcessDailyCampaignsAsync(DateTime? targetDate = null);
    }
}
