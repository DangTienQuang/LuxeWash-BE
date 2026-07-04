using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace DAL.Data
{
    public class OnnxInferenceEngine : IDisposable
    {
        private readonly InferenceSession _session;
        private const int ModelInputSize = 640;

        public OnnxInferenceEngine(string modelPath)
        {
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(modelPath, options);
        }

        public float[] RunInference(byte[] imageBytes)
        {
            var tensor = PreprocessImage(imageBytes);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", tensor)
            };
            using var results = _session.Run(inputs);
            return results.First().AsEnumerable<float>().ToArray();
        }

        private DenseTensor<float> PreprocessImage(byte[] imageBytes)
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            using var resized = bitmap.Resize(
                new SKImageInfo(ModelInputSize, ModelInputSize),
                new SKSamplingOptions(SKFilterMode.Linear));

            var tensor = new DenseTensor<float>(
                new[] { 1, 3, ModelInputSize, ModelInputSize });

            for (int y = 0; y < ModelInputSize; y++)
            {
                for (int x = 0; x < ModelInputSize; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    tensor[0, 0, y, x] = pixel.Red / 255f;
                    tensor[0, 1, y, x] = pixel.Green / 255f;
                    tensor[0, 2, y, x] = pixel.Blue / 255f;
                }
            }
            return tensor;
        }

        public void Dispose() => _session?.Dispose();
    }
}