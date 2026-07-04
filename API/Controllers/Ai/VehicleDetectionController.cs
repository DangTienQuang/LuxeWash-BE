using AutoWashPro.BLL.Exceptions;
using BLL.Services;
using BLL.Services.AI.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Ai
{
    [ApiController]
    [Route("api/lpr")]
    public class VehicleDetectionController : ControllerBase
    {
        private readonly ILicensePlateService _plateService;
        private readonly ICarRecognitionService _carRecognitionService;

        // 1. Khai báo thêm Service nhận diện xe
        private readonly ICarDetectionService _detectionService;

        public VehicleDetectionController(
            ILicensePlateService plateService,
            ICarRecognitionService carRecognitionService,
            ICarDetectionService detectionService)
        {
            _plateService = plateService;
            _carRecognitionService = carRecognitionService;
            _detectionService = detectionService; 
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
                    vehicleType = r.VehicleTypeName ?? r.PredictedVehicleType,
                    predictedBrand = r.PredictedBrand,
                    predictedModel = r.PredictedModelName,
                    confidence = r.ClassificationConfidence,
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


        [HttpPost("check-has-car")]
        public async Task<IActionResult> CheckHasCar(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return BadRequest(new { statusCode = 400, message = "Vui lòng tải lên một bức ảnh." });
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
                    message = "Đã phát hiện thấy xe ô tô trong khung hình!",
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
                    message = "Không có xe nào trong khu vực camera."
                });
            }
        }
    }
}