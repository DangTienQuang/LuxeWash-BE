using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class CarRecognitionService : ICarRecognitionService
    {
        private readonly ICarDetectionService _detectionService;
        private readonly ICarClassificationService _classificationService;
        private readonly ICarModelMatchingService _matchingService;

        public CarRecognitionService(
            ICarDetectionService detectionService,
            ICarClassificationService classificationService,
            ICarModelMatchingService matchingService)
        {
            _detectionService = detectionService;
            _classificationService = classificationService;
            _matchingService = matchingService;
        }

        public async Task<List<CarRecognitionResult>> RecognizeAsync(byte[] imageBytes)
        {
            using var original = SKBitmap.Decode(imageBytes);
            if (original == null)
                throw new InvalidOperationException("Không thể đọc ảnh đầu vào");

            var boxes = _detectionService.DetectCars(imageBytes, 0.25f);

            if (boxes.Count == 0)
            {
                boxes.Add(new CarBoundingBox
                {
                    X1 = 0,
                    Y1 = 0,
                    X2 = original.Width,
                    Y2 = original.Height,
                    Confidence = 1.0f,
                    ClassId = 0,
                    ClassName = "car"
                });
            }

            var results = new List<CarRecognitionResult>();

            foreach (var box in boxes)
            {
                var cropBytes = CropToBytes(original, box);
                var classification = _classificationService.Classify(cropBytes);

                var parts = classification.ClassName.Split('_', 2);
                var (brand, modelName) = SplitClassName(classification.ClassName);

                var match = await _matchingService.MatchOrCreatePendingAsync(brand, modelName);

                string det = box.ClassName?.ToLower() ?? "";
                string b = brand?.ToLower() ?? "";
                string model = modelName?.ToLower() ?? "";

                string predictedType;
                if (det == "pickup-truck" || det == "truck" ||
                    b.Contains("hino") || b.Contains("isuzu") || b.Contains("howo") || b.Contains("thaco") || b.Contains("dongfeng") || b.Contains("fuso") || b.Contains("kamaz") || b.Contains("jac") || b.Contains("chenglong") ||
                    model.Contains("truck") || model.Contains("tải") || model.Contains("500") || model.Contains("300") || model.Contains("ram") || model.Contains("f-150") || model.Contains("ranger") || model.Contains("hilux") || model.Contains("triton") || model.Contains("navara") || model.Contains("bt-50") || model.Contains("colorado"))
                {
                    predictedType = "Xe bán tải / Xe tải";
                }
                else if (det == "bus")
                {
                    predictedType = "Xe khách / Xe buýt";
                }
                else if (det == "motorcycle" || det == "bike" || det == "bicycle")
                {
                    predictedType = "Xe máy";
                }
                else
                {
                    predictedType = match.VehicleTypeName ?? InferVehicleType(box.ClassName, brand, modelName);
                }

                results.Add(new CarRecognitionResult
                {
                    Box = box,
                    PredictedBrand = brand,
                    PredictedModelName = modelName,
                    PredictedVehicleType = predictedType,
                    ClassificationConfidence = classification.Confidence,
                    CarModelId = match.CarModelId,
                    CarModelStatus = match.Status,
                    IsNewlyRequestedModel = match.IsNewlyCreated,
                    VehicleTypeId = match.VehicleTypeId,
                    VehicleTypeName = predictedType
                });
            }

            return results;
        }

        private string InferVehicleType(string detectorClass, string brand, string modelName)
        {
            string det = detectorClass?.ToLower() ?? "";
            string b = brand?.ToLower() ?? "";
            string model = modelName?.ToLower() ?? "";

            if (det == "motorcycle" || det == "bike" || det == "bicycle")
                return "Xe máy";

            if (det == "pickup-truck" || det == "truck" ||
                b.Contains("hino") || b.Contains("isuzu") || b.Contains("howo") || b.Contains("thaco") || b.Contains("dongfeng") || b.Contains("fuso") || b.Contains("kamaz") || b.Contains("jac") || b.Contains("chenglong") ||
                model.Contains("truck") || model.Contains("tải") || model.Contains("500") || model.Contains("300") || model.Contains("ram") || model.Contains("f-150") || model.Contains("ranger") || model.Contains("hilux") || model.Contains("triton") || model.Contains("navara") || model.Contains("bt-50") || model.Contains("colorado"))
                return "Xe bán tải / Xe tải";

            if (det == "bus")
                return "Xe khách / Xe buýt";

            if (det == "suv" || det == "van" || model.Contains("explorer") || model.Contains("wrangler") || model.Contains("fortuner") || model.Contains("everest") || model.Contains("santa fe") || model.Contains("sorento") || model.Contains("cr-v") || model.Contains("cx-8") || model.Contains("innova") || model.Contains("carnival") || model.Contains("xpander") || model.Contains("rush") || model.Contains("veloz"))
                return "Xe 7 chỗ (SUV/MPV)";

            return "Xe 4 chỗ (Sedan/Hatchback)";
        }

        private byte[] CropToBytes(SKBitmap original, CarBoundingBox box)
        {
            float bw = box.X2 - box.X1;
            float bh = box.Y2 - box.Y1;

            float expandX = bw * 0.08f;
            float expandY = bh * 0.08f;

            int x1 = Math.Max(0, (int)(box.X1 - expandX));
            int y1 = Math.Max(0, (int)(box.Y1 - expandY));
            int x2 = Math.Min(original.Width, (int)(box.X2 + expandX));
            int y2 = Math.Min(original.Height, (int)(box.Y2 + expandY));

            if (x2 <= x1 || y2 <= y1)
            {
                x1 = 0; y1 = 0; x2 = original.Width; y2 = original.Height;
            }

            var cropRect = new SKRectI(x1, y1, x2, y2);
            using var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(original, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height), new SKSamplingOptions(SKFilterMode.Linear));

            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
            return data.ToArray();
        }

        private static readonly string[] KnownMultiWordBrands = { "Mercedes Benz", "Chevrolet", "Volkswagen" };

        private (string Brand, string Model) SplitClassName(string className)
        {
            foreach (var brand in KnownMultiWordBrands.OrderByDescending(b => b.Length))
            {
                if (className.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = className.Substring(brand.Length).Trim();
                    return (brand, string.IsNullOrEmpty(rest) ? "Unknown" : rest);
                }
            }

            // Fallback: first word is brand, rest is model
            var firstSpace = className.IndexOf(' ');
            if (firstSpace < 0) return (className, "Unknown");

            return (className[..firstSpace], className[(firstSpace + 1)..].Trim());
        }
    }

}
