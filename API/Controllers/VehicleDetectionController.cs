using AutoWashPro.BLL.Exceptions;
using BLL.Services;
using BLL.Services.AI.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/lpr")]
    public class VehicleDetectionController : ControllerBase
    {
        private readonly ILicensePlateService _plateService;
        private readonly ICarRecognitionService _carRecognitionService;

        public VehicleDetectionController(ILicensePlateService plateService, ICarRecognitionService carRecognitionService)
        {
            _plateService = plateService;
            _carRecognitionService = carRecognitionService;
        }

        [HttpPost("detect-plate")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> DetectPlate(IFormFile image)
        {
            if (image == null || image.Length == 0)
                throw new BadRequestException("Vui lòng cung cấp ảnh.");

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);

            var result = await _plateService.DetectPlateAsync(ms.ToArray());

            if (!result.Detected)
                throw new NotFoundException("Không phát hiện được biển số.");

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = new
                {
                    plateText = result.PlateText,
                    confidence = result.Confidence
                }
            });
        }

        [HttpPost("detect-dual-plate")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> DetectDualPlate(IFormFile? frontImage, IFormFile? backImage)
        {
            if (frontImage == null && backImage == null)
                throw new BadRequestException("Cần cung cấp ít nhất một ảnh.");

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
                throw new NotFoundException("Không phát hiện được biển số.");

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
        public async Task<IActionResult> Recognize(IFormFile image)
        {
            if (image == null || image.Length == 0)
                throw new InvalidOperationException("Vui lòng tải lên ảnh xe");

            using var ms = new MemoryStream();
            await image.CopyToAsync(ms);

            var results = await _carRecognitionService.RecognizeAsync(ms.ToArray());

            return Ok(new
            {
                statusCode = 200,
                message = "Nhận diện xe thành công",
                data = results.Select(r => new
                {
                    box = new { r.Box.X1, r.Box.Y1, r.Box.X2, r.Box.Y2, r.Box.Confidence },
                    predictedBrand = r.PredictedBrand,
                    predictedModel = r.PredictedModelName,
                    confidence = r.ClassificationConfidence,
                    carModelId = r.CarModelId,
                    carModelStatus = r.CarModelStatus,
                    isNewlyRequestedModel = r.IsNewlyRequestedModel
                })
            });
        }
    }
}