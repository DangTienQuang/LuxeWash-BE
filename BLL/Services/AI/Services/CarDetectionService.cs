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
        private readonly int _inputWidth = 640;
        private readonly int _inputHeight = 640;

        public CarDetectionService(IConfiguration config)
        {
            var modelPath = config["AiModels:VehicleDetectorPath"]
                ?? throw new InvalidOperationException("Thiếu cấu hình đường dẫn model nhận diện xe (VehicleDetectorPath)");

            var options = new SessionOptions();
            _session = new InferenceSession(modelPath, options);
            _classNames = OnnxMetadataHelper.ExtractClassNames(_session);

            var inputMeta = _session.InputMetadata.Values.FirstOrDefault();
            if (inputMeta != null && inputMeta.Dimensions.Length >= 4)
            {
                if (inputMeta.Dimensions[2] > 0) _inputHeight = inputMeta.Dimensions[2];
                if (inputMeta.Dimensions[3] > 0) _inputWidth = inputMeta.Dimensions[3];
            }

            //Console.WriteLine($"Detector classes: {string.Join(", ", _classNames)}");
        }

        public List<CarBoundingBox> DetectCars(byte[] imageBytes, float confidenceThreshold = 0.25f, float iouThreshold = 0.45f)
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

            return PostProcess(output, scaleX, scaleY, confidenceThreshold, iouThreshold, original.Width, original.Height);
        }

        private (DenseTensor<float> tensor, float scaleX, float scaleY) Preprocess(SKBitmap original)
        {
            using var resized = original.Resize(new SKImageInfo(_inputWidth, _inputHeight), SKFilterQuality.High);

            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });

            for (int y = 0; y < _inputHeight; y++)
            {
                for (int x = 0; x < _inputWidth; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    tensor[0, 0, y, x] = pixel.Red / 255f;
                    tensor[0, 1, y, x] = pixel.Green / 255f;
                    tensor[0, 2, y, x] = pixel.Blue / 255f;
                }
            }

            float scaleX = original.Width / (float)_inputWidth;
            float scaleY = original.Height / (float)_inputHeight;

            return (tensor, scaleX, scaleY);
        }

        private static readonly HashSet<string> AcceptedClasses = new(StringComparer.OrdinalIgnoreCase)
            {
                "car", "pickup-truck", "vehicles", "vehicle", "bus", "truck", "suv", "van", "motorcycle", "bike", "bicycle", "automobile", "sedan", "hatchback", "mpv", "jeep", "minivan"
            };

        private List<CarBoundingBox> PostProcess(Tensor<float> output, float scaleX, float scaleY, float confThreshold, float iouThreshold, int imgWidth, int imgHeight)
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

                string? className = bestClassId >= 0 && bestClassId < _classNames.Count ? _classNames[bestClassId] : "car";

                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                float x1, y1, x2, y2;
                if (cx <= 1.5f && cy <= 1.5f && w <= 1.5f && h <= 1.5f)
                {
                    x1 = (cx - w / 2f) * imgWidth;
                    y1 = (cy - h / 2f) * imgHeight;
                    x2 = (cx + w / 2f) * imgWidth;
                    y2 = (cy + h / 2f) * imgHeight;
                }
                else
                {
                    x1 = (cx - w / 2f) * scaleX;
                    y1 = (cy - h / 2f) * scaleY;
                    x2 = (cx + w / 2f) * scaleX;
                    y2 = (cy + h / 2f) * scaleY;
                }

                candidates.Add(new CarBoundingBox
                {
                    X1 = Math.Max(0, x1),
                    Y1 = Math.Max(0, y1),
                    X2 = Math.Min(imgWidth, x2),
                    Y2 = Math.Min(imgHeight, y2),
                    Confidence = bestScore,
                    ClassId = bestClassId,
                    ClassName = className ?? "car"
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
