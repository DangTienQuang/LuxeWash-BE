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
            var boxes = _detectionService.DetectCars(imageBytes);
            if (boxes.Count == 0)
                return new List<CarRecognitionResult>();

            using var original = SKBitmap.Decode(imageBytes);
            if (original == null)
                throw new InvalidOperationException("Không thể đọc ảnh đầu vào");

            var results = new List<CarRecognitionResult>();

            foreach (var box in boxes)
            {
                var cropBytes = CropToBytes(original, box);
                var classification = _classificationService.Classify(cropBytes);

                var parts = classification.ClassName.Split('_', 2);
                var (brand, modelName) = SplitClassName(classification.ClassName);

                var match = await _matchingService.MatchOrCreatePendingAsync(brand, modelName);

                results.Add(new CarRecognitionResult
                {
                    Box = box,
                    PredictedBrand = brand,
                    PredictedModelName = modelName,
                    ClassificationConfidence = classification.Confidence,
                    CarModelId = match.CarModelId,
                    CarModelStatus = match.Status,
                    IsNewlyRequestedModel = match.IsNewlyCreated
                });
            }

            return results;
        }

        private byte[] CropToBytes(SKBitmap original, CarBoundingBox box)
        {
            int x1 = Math.Max(0, (int)box.X1);
            int y1 = Math.Max(0, (int)box.Y1);
            int x2 = Math.Min(original.Width, (int)box.X2);
            int y2 = Math.Min(original.Height, (int)box.Y2);

            var cropRect = new SKRectI(x1, y1, x2, y2);
            using var cropped = new SKBitmap(cropRect.Width, cropRect.Height);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(original, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));

            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
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
