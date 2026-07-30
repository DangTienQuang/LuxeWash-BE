using System.Threading.Tasks;

namespace BLL.Services.AI.Interfaces
{
    public interface IVisionFeedbackService
    {
        /// <summary>
        /// Saves a vehicle vision feedback entry (used for AI training data collection).
        /// </summary>
        Task<int> SaveFeedbackAsync(
            string licensePlate,
            string imageUrl,
            int predictedVehicleTypeId,
            int actualVehicleTypeId,
            string? actualBrand,
            string? actualModel);
    }
}
