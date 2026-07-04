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
        private static readonly Dictionary<int, string> CocoVehicleClasses = new()
        {
            { 2, "car" },
            { 3, "motorcycle" },
            { 5, "bus" },
            { 7, "truck" }
        };

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

            var (inputTensor, scale, padX, padY) = Preprocess(original);

            var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), inputTensor)
                };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            return PostProcess(output, scale, padX, padY, confidenceThreshold, original.Width, original.Height);
        }

        private (DenseTensor<float> tensor, float scale, int padX, int padY) Preprocess(SKBitmap original)
        {
            float scale = Math.Min((float)_inputWidth / original.Width, (float)_inputHeight / original.Height);
            int newWidth = (int)(original.Width * scale);
            int newHeight = (int)(original.Height * scale);
            int padX = (_inputWidth - newWidth) / 2;
            int padY = (_inputHeight - newHeight) / 2;

            using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear));

            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });

            for (int y = 0; y < _inputHeight; y++)
                for (int x = 0; x < _inputWidth; x++)
                {
                    tensor[0, 0, y, x] = 114f / 255f;
                    tensor[0, 1, y, x] = 114f / 255f;
                    tensor[0, 2, y, x] = 114f / 255f;
                }

            for (int y = 0; y < newHeight; y++)
                for (int x = 0; x < newWidth; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    tensor[0, 0, y + padY, x + padX] = pixel.Red / 255f;
                    tensor[0, 1, y + padY, x + padX] = pixel.Green / 255f;
                    tensor[0, 2, y + padY, x + padX] = pixel.Blue / 255f;
                }

            return (tensor, scale, padX, padY);
        }

        private static readonly HashSet<string> AcceptedClasses = new(StringComparer.OrdinalIgnoreCase)
            {
                "car", "pickup-truck", "vehicles", "vehicle", "bus", "truck", "suv", "van", "motorcycle", "bike", "bicycle", "automobile", "sedan", "hatchback", "mpv", "jeep", "minivan"
            };

        private List<CarBoundingBox> PostProcess(Tensor<float> output, float scale, int padX, int padY, float confThreshold, int origWidth, int origHeight)
        {
            var results = new List<CarBoundingBox>();
            int numDetections = (int)output.Dimensions[1]; // 300

            for (int i = 0; i < numDetections; i++)
            {
                float confidence = output[0, i, 4];
                if (confidence < confThreshold) break;

                int classId = (int)output[0, i, 5];
                if (!CocoVehicleClasses.TryGetValue(classId, out var className)) continue;

                float x1 = (output[0, i, 0] - padX) / scale;
                float y1 = (output[0, i, 1] - padY) / scale;
                float x2 = (output[0, i, 2] - padX) / scale;
                float y2 = (output[0, i, 3] - padY) / scale;

                results.Add(new CarBoundingBox
                {
                    X1 = Math.Max(0f, x1),
                    Y1 = Math.Max(0f, y1),
                    X2 = Math.Min((float)origWidth, x2),
                    Y2 = Math.Min((float)origHeight, y2),
                    Confidence = confidence,
                    ClassId = classId,
                    ClassName = className
                });
            }

            return results;
        }

        public void Dispose() => _session?.Dispose();
    }
}
