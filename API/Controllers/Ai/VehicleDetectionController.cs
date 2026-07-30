using AutoWashPro.BLL.Exceptions;
using BLL.Services;
using BLL.Services.AI.Interfaces;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers.Ai
{
    [ApiController]
    [Route("api/lpr")]
    public class VehicleDetectionController : ControllerBase
    {
        private readonly ILicensePlateService _plateService;
        private readonly ICarRecognitionService _carRecognitionService;
        private readonly ICarDetectionService _detectionService;
        private readonly AutoWashPro.DAL.Data.AutoWashDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public VehicleDetectionController(
            ILicensePlateService plateService,
            ICarRecognitionService carRecognitionService,
            ICarDetectionService detectionService,
            AutoWashPro.DAL.Data.AutoWashDbContext context,
            ICloudinaryService cloudinaryService)
        {
            _plateService = plateService;
            _carRecognitionService = carRecognitionService;
            _detectionService = detectionService;
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        [HttpPost("detect-plate")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> DetectPlate(IFormFile image)
        {
            if (image == null || image.Length == 0)
                throw new BadRequestException("Please provide an image.");

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);

            var result = await _plateService.DetectPlateAsync(ms.ToArray());

            if (!result.Detected)
                throw new NotFoundException("License plate could not be detected.");

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = new
                {
                    plateText = result.PlateText,
                    plateTexts = result.PlateTexts,
                    confidence = result.Confidence
                }
            });
        }

        [HttpPost("detect-dual-plate")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> DetectDualPlate(IFormFile? frontImage, IFormFile? backImage)
        {
            if (frontImage == null && backImage == null)
                throw new BadRequestException("At least one image must be provided.");

            byte[]? frontBytes = null;
            byte[]? backBytes = null;

            if (frontImage != null)
            {
                using var ms = new MemoryStream();
                await frontImage.CopyToAsync(ms);
                frontBytes = ms.ToArray();
            }

            if (backImage != null)
            {
                using var ms = new MemoryStream();
                await backImage.CopyToAsync(ms);
                backBytes = ms.ToArray();
            }

            var result = await _plateService.DetectDualPlateAsync(frontBytes, backBytes);

            if (!result.Detected)
                throw new NotFoundException("License plate could not be detected.");

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = new
                {
                    plateText = result.FinalPlateText,
                    confirmedBy = result.ConfirmedBy,
                    front = result.Front == null ? null : (object)new
                    {
                        detected = result.Front.Detected,
                        plateText = result.Front.PlateText,
                        confidence = result.Front.Confidence,
                        plateType = result.Front.PlateType
                    },
                    back = result.Back == null ? null : (object)new
                    {
                        detected = result.Back.Detected,
                        plateText = result.Back.PlateText,
                        confidence = result.Back.Confidence,
                        plateType = result.Back.PlateType
                    }
                }
            });
        }

        [HttpPost("car-recognize")]
        public async Task<IActionResult> Recognize(IFormFile image, [FromForm] string? licensePlate)
        {
            if (image == null || image.Length == 0)
                throw new InvalidOperationException("Please upload a vehicle image");

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);

            var results = await _carRecognitionService.RecognizeAsync(ms.ToArray());

            bool isOverriddenByHistory = false;
            string? historicalVehicleTypeName = null;

            if (!string.IsNullOrWhiteSpace(licensePlate))
            {
                var normalizedPlate = licensePlate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper();

                var customerVehicle = await _context.Vehicles
                    .Include(v => v.VehicleType)
                    .Where(v => v.LicensePlate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedPlate && !v.IsDeleted)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (customerVehicle != null)
                {
                    historicalVehicleTypeName = customerVehicle.VehicleType?.Name;
                    isOverriddenByHistory = true;
                }
                else
                {
                    var fleetVehicle = await _context.FleetVehicles
                        .Include(fv => fv.VehicleType)
                        .Where(fv => fv.LicensePlate.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == normalizedPlate && fv.Status == "Active")
                        .AsNoTracking()
                        .FirstOrDefaultAsync();

                    if (fleetVehicle != null)
                    {
                        historicalVehicleTypeName = fleetVehicle.VehicleType?.Name;
                        isOverriddenByHistory = true;
                    }
                }
            }

            return Ok(new
            {
                statusCode = 200,
                message = "Vehicle recognized successfully",
                isOverriddenByHistory = isOverriddenByHistory,
                data = results.Select(r => new
                {
                    vehicleType = historicalVehicleTypeName ?? r.VehicleTypeName ?? r.PredictedVehicleType,
                    predictedBrand = r.PredictedBrand,
                    predictedModel = r.PredictedModelName,
                    confidence = isOverriddenByHistory ? 1.0f : r.ClassificationConfidence,
                    box = r.Box != null ? new
                    {
                        x1 = r.Box.X1,
                        y1 = r.Box.Y1,
                        x2 = r.Box.X2,
                        y2 = r.Box.Y2
                    } : null
                })
            });
        }

        [HttpPost("feedback")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> SubmitFeedback(
            IFormFile image, 
            [FromForm] string licensePlate, 
            [FromForm] int? predictedVehicleTypeId, 
            [FromForm] int actualVehicleTypeId,
            [FromForm] string? actualBrand = null,
            [FromForm] string? actualModel = null)
        {
            if (image == null || image.Length == 0)
                throw new BadRequestException("Please provide an image.");
            
            if (string.IsNullOrWhiteSpace(licensePlate))
                throw new BadRequestException("License plate is required.");

            // Upload image to Cloudinary
            string imageUrl = await _cloudinaryService.UploadFileAsync(image, "ai-vision-feedback");

            // Save feedback log
            var feedback = new AutoWashPro.DAL.Entities.VehicleVisionFeedback
            {
                LicensePlate = licensePlate,
                ImageUrl = imageUrl,
                PredictedVehicleTypeId = predictedVehicleTypeId,
                ActualVehicleTypeId = actualVehicleTypeId,
                ActualBrand = actualBrand,
                ActualModel = actualModel,
                CreatedAt = DateTime.UtcNow
            };

            _context.VehicleVisionFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                statusCode = 200,
                message = "Feedback submitted successfully for AI training.",
                data = new { feedbackId = feedback.FeedbackId, imageUrl = imageUrl }
            });
        }


        [HttpPost("check-has-car")]
        public async Task<IActionResult> CheckHasCar(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return BadRequest(new { statusCode = 400, message = "Please upload an image." });
            }

            using var memoryStream = new MemoryStream();
            await imageFile.CopyToAsync(memoryStream);
            byte[] imageBytes = memoryStream.ToArray();


            var boundingBoxes = _detectionService.DetectCars(imageBytes, 0.25f);

            if (boundingBoxes.Count > 0)
            {
                return Ok(new
                {
                    statusCode = 200,
                    success = true,
                    hasCar = true,
                    carCount = boundingBoxes.Count,
                    message = "Vehicle detected in frame!",
                    boxes = boundingBoxes
                });
            }
            else
            {
                return Ok(new
                {
                    statusCode = 200,
                    success = true,
                    hasCar = false,
                    carCount = 0,
                    message = "No vehicle detected in camera area."
                });
            }
        }
    }
}