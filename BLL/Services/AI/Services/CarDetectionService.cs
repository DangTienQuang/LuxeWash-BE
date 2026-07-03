using BLL.Services.AI.Helpers;
using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class CarDetectionService : ICarDetectionService, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly List<string> _classNames;
        private const int InputSize = 416;

        public CarDetectionService(IConfiguration config)
        {
            var modelPath = config["AiModels:VehicleDetectorPath"]
                ?? throw new InvalidOperationException("Thiếu cấu hình đường dẫn model nhận diện xe (VehicleDetectorPath)");

            var options = new SessionOptions();
            _session = new InferenceSession(modelPath, options);
            _classNames = OnnxMetadataHelper.ExtractClassNames(_session);

            //Console.WriteLine($"Detector classes: {string.Join(", ", _classNames)}");
        }

        public List<CarBoundingBox> DetectCars(byte[] imageBytes, float confidenceThreshold = 0.4f, float iouThreshold = 0.45f)
        {
            using var original = SKBitmap.Decode(imageBytes);
            if (original == null)
                throw new InvalidOperationException("Không thể đọc ảnh đầu vào");

            var (inputTensor, scaleX, scaleY) = Preprocess(original);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), inputTensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            return PostProcess(output, scaleX, scaleY, confidenceThreshold, iouThreshold);
        }

        private (DenseTensor<float> tensor, float scaleX, float scaleY) Preprocess(SKBitmap original)
        {
            using var resized = original.Resize(new SKImageInfo(InputSize, InputSize), SKFilterQuality.Medium);

            var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });

            for (int y = 0; y < InputSize; y++)
            {
                for (int x = 0; x < InputSize; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    tensor[0, 0, y, x] = pixel.Red / 255f;
                    tensor[0, 1, y, x] = pixel.Green / 255f;
                    tensor[0, 2, y, x] = pixel.Blue / 255f;
                }
            }

            float scaleX = original.Width / (float)InputSize;
            float scaleY = original.Height / (float)InputSize;

            return (tensor, scaleX, scaleY);
        }

        private static readonly HashSet<string> AcceptedClasses = new(StringComparer.OrdinalIgnoreCase)
            {
                "car"
            };

        private List<CarBoundingBox> PostProcess(Tensor<float> output, float scaleX, float scaleY, float confThreshold, float iouThreshold)
        {
            int numAttrs = (int)output.Dimensions[1];
            int numClasses = numAttrs - 4;
            int numAnchors = (int)output.Dimensions[2];

            var candidates = new List<CarBoundingBox>();

            for (int i = 0; i < numAnchors; i++)
            {
                float bestScore = 0f;
                int bestClassId = -1;

                for (int c = 0; c < numClasses; c++)
                {
                    float score = output[0, 4 + c, i];
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClassId = c;
                    }
                }

                if (bestScore < confThreshold) continue;

                string? className = bestClassId >= 0 && bestClassId < _classNames.Count ? _classNames[bestClassId] : null;

                // Filter irrelevant classes out BEFORE NMS, not after
                if (className == null || !AcceptedClasses.Contains(className))
                    continue;

                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                candidates.Add(new CarBoundingBox
                {
                    X1 = (cx - w / 2f) * scaleX,
                    Y1 = (cy - h / 2f) * scaleY,
                    X2 = (cx + w / 2f) * scaleX,
                    Y2 = (cy + h / 2f) * scaleY,
                    Confidence = bestScore,
                    ClassId = bestClassId,
                    ClassName = className
                });
            }

            return NonMaxSuppression(candidates, iouThreshold);
        }
        private List<CarBoundingBox> NonMaxSuppression(List<CarBoundingBox> boxes, float iouThreshold)
        {
            var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();
            var keep = new List<CarBoundingBox>();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                keep.Add(best);
                sorted.RemoveAt(0);

                sorted.RemoveAll(b => IoU(best, b) > iouThreshold);
            }

            return keep;
        }

        private float IoU(CarBoundingBox a, CarBoundingBox b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);

            float interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);

            return interArea / (areaA + areaB - interArea + 1e-6f);
        }

        public void Dispose() => _session?.Dispose();
    }
}
