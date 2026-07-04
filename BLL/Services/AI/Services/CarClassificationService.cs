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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class CarClassificationService : ICarClassificationService, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly List<string> _classNames;
        private const int InputSize = 224;

        public CarClassificationService(IConfiguration config)
        {
            var modelPath = config["AiModels:CarClassifierPath"]
                ?? throw new InvalidOperationException("Thiếu cấu hình đường dẫn model phân loại xe (CarClassifierPath)");

            _session = new InferenceSession(modelPath, new SessionOptions());
            _classNames = OnnxMetadataHelper.ExtractClassNames(_session);
        }

        public CarClassificationResult Classify(byte[] croppedCarImage)
        {
            using var bitmap = SKBitmap.Decode(croppedCarImage);
            if (bitmap == null)
                throw new InvalidOperationException("Không thể đọc ảnh xe đã cắt");

            var inputTensor = Preprocess(bitmap);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), inputTensor)
            };

            using var results = _session.Run(inputs);
            var logits = results.First().AsTensor<float>().ToArray();

            var probabilities = Softmax(logits);
            int bestIndex = Array.IndexOf(probabilities, probabilities.Max());

            return new CarClassificationResult
            {
                ClassName = bestIndex < _classNames.Count ? _classNames[bestIndex] : "unknown",
                Confidence = probabilities[bestIndex]
            };
        }

        private DenseTensor<float> Preprocess(SKBitmap bitmap)
        {
            using var resized = bitmap.Resize(new SKImageInfo(InputSize, InputSize), new SKSamplingOptions(SKFilterMode.Linear));
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

            return tensor;
        }

        private float[] Softmax(float[] logits)
        {
            float max = logits.Max();
            var exps = logits.Select(l => MathF.Exp(l - max)).ToArray();
            float sum = exps.Sum();
            return exps.Select(e => e / sum).ToArray();
        }

        public void Dispose() => _session?.Dispose();
    }
}
