using BLL.DTOs;
namespace BLL.Services
{
    public interface IAIChatbotService
    {
        Task<AIChatResponseDTO> ChatAsync(
            int userId,
            AIChatRequestDTO request);
        Task<string> GetRecommendationAsync(
            int userId);
    }
}