using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface ICRMCampaignService
    {
        Task<List<VoucherCampaignProcessResultDTO>> ProcessDailyCampaignsAsync();
        Task<string> TriggerWeatherCampaignAsync();
        Task<string> TriggerSmartWeatherCampaignAsync();
        Task<string> SimulateSmartWeatherCampaignAsync(WeatherCampaignSimulationRequestDTO request);
    }
}
