namespace AutoWashPro.BLL.DTOs
{
    public class WeatherCampaignSimulationRequestDTO
    {
        public int BranchId { get; set; }
        public bool IsProlongedRain { get; set; }
        public double OccupancyRate { get; set; }
    }
}
