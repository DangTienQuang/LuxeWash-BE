using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Services.AI.Interfaces;
using System;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class VisionFeedbackService : IVisionFeedbackService
    {
        private readonly AutoWashDbContext _context;

        public VisionFeedbackService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveFeedbackAsync(
            string licensePlate,
            string imageUrl,
            int predictedVehicleTypeId,
            int actualVehicleTypeId,
            string? actualBrand,
            string? actualModel)
        {
            var feedback = new VehicleVisionFeedback
            {
                LicensePlate = licensePlate,
                ImageUrl = imageUrl,
                PredictedVehicleTypeId = predictedVehicleTypeId,
                ActualVehicleTypeId = actualVehicleTypeId,
                ActualBrand = actualBrand,
                ActualModel = actualModel,
                CreatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow
            };

            _context.VehicleVisionFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();
            return feedback.FeedbackId;
        }
    }
}
