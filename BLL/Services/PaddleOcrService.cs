using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace BLL.Services
{
    public class PaddleOcrService : IDisposable
    {
        private readonly InferenceSession _recSession;
        private readonly ILogger<PaddleOcrService> _logger;
        private readonly string _charSet;

        public PaddleOcrService(string recModelPath, string dictPath, ILogger<PaddleOcrService> logger)
        {
            _logger = logger;
            var options = new SessionOptions();
            options.GraphOptimizationLevel =
                GraphOptimizationLevel.ORT_ENABLE_ALL;

            _recSession = new InferenceSession(recModelPath, options);

            var chars = File.ReadAllLines(dictPath)
                            .Select(l => l.TrimEnd('\r', '\n'))
                            .ToList();

            _charSet = string.Join("", chars);
            _logger.LogInformation(
                "Charset loaded: {Size} chars. First='{F}' Last='{L}'",
                _charSet.Length,
                _charSet.FirstOrDefault(),
                _charSet.LastOrDefault());
        }

        // ── Public entry point ───────────────────────────────────────────────
        public async Task<string> ExtractTextAsync(byte[] imageBytes, string plateType = "SHORT", string position = "Unknown")
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation(
                        "ExtractTextAsync — plateType: {Type} position: {Pos}",
                        plateType, position);

                    if (plateType != "LONG")
                    {
                        using var testBmp = SKBitmap.Decode(imageBytes);
                        if (testBmp != null && !IsTwoLinePlate(testBmp))
                        {
                            _logger.LogInformation(
                                "[{Pos}] Auto-detected 1-line layout (override SHORT → LONG)", position);
                            return ReadLongPlate(imageBytes, position);
                        }
                    }

                    if (plateType == "LONG")
                        return ReadLongPlate(imageBytes, position);
                    else
                        return ReadTwoLinePlate(imageBytes, position);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PaddleOCR failed: {Msg}", ex.Message);
                    return string.Empty;
                }
            });
        }

        private bool IsTwoLinePlate(SKBitmap bmp)
        {
            int W = bmp.Width;
            int H = bmp.Height;
            float[] rowEdges = new float[H];
            float maxRowEdge = 0f;
            for (int y = 0; y < H; y++)
            {
                float edges = 0f;
                for (int x = 1; x < W; x++)
                {
                    var p1 = bmp.GetPixel(x, y);
                    var p0 = bmp.GetPixel(x - 1, y);
                    edges += Math.Abs((p1.Red + p1.Green + p1.Blue) - (p0.Red + p0.Green + p0.Blue));
                }
                rowEdges[y] = edges / (W * 3f * 255f);
                if (rowEdges[y] > maxRowEdge) maxRowEdge = rowEdges[y];
            }

            if (maxRowEdge < 0.01f) return false;

            int win = Math.Max(2, H / 30);
            float[] smoothed = new float[H];
            for (int y = 0; y < H; y++)
            {
                float sum = 0f;
                int count = 0;
                for (int dy = -win; dy <= win; dy++)
                {
                    if (y + dy >= 0 && y + dy < H) { sum += rowEdges[y + dy]; count++; }
                }
                smoothed[y] = sum / count;
            }

            float th = maxRowEdge * 0.30f;
            int peakCount = 0;
            bool inPeak = false;
            for (int y = 0; y < H; y++)
            {
                if (smoothed[y] > th)
                {
                    if (!inPeak) { peakCount++; inPeak = true; }
                }
                else if (smoothed[y] < maxRowEdge * 0.18f)
                {
                    inPeak = false;
                }
            }

            return peakCount >= 2;
        }

        // ── One-line plate (biển dài) ────────────────────────────────────────
        private string ReadLongPlate(byte[] imageBytes, string position)
        {
            using var rawBmp = SKBitmap.Decode(imageBytes);
            if (rawBmp == null) return string.Empty;

            var lineText = RecognizeTextLine(rawBmp, position + "_long");
            _logger.LogInformation("[{Pos}] LONG plate result: '{T}'", position, lineText);

            return lineText.ToUpper();
        }

        // ── Two-line plate (biển ngắn) ───────────────────────────────────────
        private string ReadTwoLinePlate(byte[] imageBytes, string position)
        {
            using var rawBmp = SKBitmap.Decode(imageBytes);
            if (rawBmp == null) return string.Empty;

            // Scale up for high-precision edge profile splitting without arbitrary padding
            int scale = Math.Max(3, 300 / Math.Max(rawBmp.Width, 1));
            int W = rawBmp.Width * scale;
            int H = rawBmp.Height * scale;

            var samplingOptions = new SKSamplingOptions(SKCubicResampler.Mitchell);
            using var upscaled = rawBmp.Resize(new SKImageInfo(W, H), samplingOptions);

            // Search for valley (minimum horizontal transition density) between 32% and 68% height
            int searchStart = (int)(H * 0.32f);
            int searchEnd = (int)(H * 0.68f);

            float[] edgeRow = new float[H];
            for (int y = 0; y < H; y++)
            {
                float edges = 0f;
                for (int x = 1; x < W; x++)
                {
                    var p1 = upscaled.GetPixel(x, y);
                    var p0 = upscaled.GetPixel(x - 1, y);
                    edges += Math.Abs((p1.Red + p1.Green + p1.Blue) - (p0.Red + p0.Green + p0.Blue));
                }
                edgeRow[y] = edges / (W * 3f * 255f);
            }

            int win = Math.Max(2, H / 30);
            float minEdge = float.MaxValue;
            int splitY = H / 2;

            for (int y = searchStart; y < searchEnd; y++)
            {
                float sum = 0f;
                int count = 0;
                for (int dy = -win; dy <= win; dy++)
                {
                    if (y + dy >= 0 && y + dy < H)
                    {
                        sum += edgeRow[y + dy];
                        count++;
                    }
                }
                float smoothed = sum / count;
                if (smoothed < minEdge)
                {
                    minEdge = smoothed;
                    splitY = y;
                }
            }

            _logger.LogInformation(
                "[{Pos}] Smart split at Y={S}/{H} (min edge activity={E:F4})",
                position, splitY, H, minEdge);

            using var topBmp = new SKBitmap(W, splitY);
            using var botBmp = new SKBitmap(W, H - splitY);

            upscaled.ExtractSubset(topBmp, new SKRectI(0, 0, W, splitY));
            upscaled.ExtractSubset(botBmp, new SKRectI(0, splitY, W, H));

            var line1 = RecognizeTextLine(topBmp, position + "_line1");
            var line2 = RecognizeTextLine(botBmp, position + "_line2");

            _logger.LogInformation("[{Pos}] Line 1: '{A}'", position, line1);
            _logger.LogInformation("[{Pos}] Line 2: '{B}'", position, line2);

            return (line1 + line2).ToUpper();
        }

        // ── Text line preprocessing & recognition ────────────────────────────
        private string RecognizeTextLine(SKBitmap sourceBmp, string debugName)
        {
            using var cropped = AutoCropTextRegion(sourceBmp);
            if (cropped.Width < 5 || cropped.Height < 5) return string.Empty;

            byte[] ToBytes(SKBitmap bmp)
            {
                using var ms = new MemoryStream();
                bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
                return ms.ToArray();
            }

            try
            {
                File.WriteAllBytes(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"debug_{debugName}.png"),
                    ToBytes(cropped));
            }
            catch { /* Ignore debug save errors */ }

            return RunOnnxRecognition(cropped);
        }

        private SKBitmap AutoCropTextRegion(SKBitmap bmp)
        {
            int W = bmp.Width;
            int H = bmp.Height;

            // Compute horizontal transition profile
            float[] rowEdges = new float[H];
            float maxRowEdge = 0f;
            for (int y = 0; y < H; y++)
            {
                float edges = 0f;
                for (int x = 1; x < W; x++)
                {
                    var p1 = bmp.GetPixel(x, y);
                    var p0 = bmp.GetPixel(x - 1, y);
                    edges += Math.Abs((p1.Red + p1.Green + p1.Blue) - (p0.Red + p0.Green + p0.Blue));
                }
                rowEdges[y] = edges / (W * 3f * 255f);
                if (rowEdges[y] > maxRowEdge) maxRowEdge = rowEdges[y];
            }

            if (maxRowEdge < 0.01f) return bmp.Copy();

            float thresholdY = maxRowEdge * 0.27f;
            int minY = 0;
            while (minY < H - 1 && rowEdges[minY] < thresholdY) minY++;

            int maxY = H - 1;
            while (maxY > minY && rowEdges[maxY] < thresholdY) maxY--;

            int padY = Math.Max(2, (maxY - minY) / 20);
            minY = Math.Max(0, minY - padY);
            maxY = Math.Min(H - 1, maxY + padY);

            if (maxY <= minY + 2) return bmp.Copy();

            var cropped = new SKBitmap(W, maxY - minY + 1);
            bmp.ExtractSubset(cropped, new SKRectI(0, minY, W, maxY + 1));
            return cropped;
        }

        // ── Recognition ──────────────────────────────────────────────────────
        private string RunOnnxRecognition(SKBitmap bitmap)
        {
            int recH = 48; // PP-OCRv3/v4 recognition models standard input height
            float ratio = (float)bitmap.Width / Math.Max(bitmap.Height, 1);
            int recW = Math.Max(10, (int)(recH * ratio));

            var samplingOptions = new SKSamplingOptions(SKCubicResampler.Mitchell);
            using var resized = bitmap.Resize(new SKImageInfo(recW, recH), samplingOptions);

            var tensor = new DenseTensor<float>(new[] { 1, 3, recH, recW });

            for (int y = 0; y < recH; y++)
            {
                for (int x = 0; x < recW; x++)
                {
                    var p = resized.GetPixel(x, y);
                    tensor[0, 0, y, x] = (p.Red / 255f - 0.5f) / 0.5f;
                    tensor[0, 1, y, x] = (p.Green / 255f - 0.5f) / 0.5f;
                    tensor[0, 2, y, x] = (p.Blue / 255f - 0.5f) / 0.5f;
                }
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("x", tensor)
            };

            using var results = _recSession.Run(inputs);
            var output = results
                .First(r => r.Name == "softmax_2.tmp_0")
                .AsTensor<float>();

            return DecodeCtc(output);
        }

        // ── CTC Decoder ──────────────────────────────────────────────────────
        private string DecodeCtc(Tensor<float> output)
        {
            int T = output.Dimensions[1];
            int numClasses = output.Dimensions[2];

            var sb = new System.Text.StringBuilder();
            int lastIdx = -1;

            for (int t = 0; t < T; t++)
            {
                int maxIdx = 0;
                float maxVal = float.MinValue;

                for (int c = 0; c < numClasses; c++)
                {
                    if (output[0, t, c] > maxVal)
                    {
                        maxVal = output[0, t, c];
                        maxIdx = c;
                    }
                }

                if (maxIdx != 0 && maxIdx != lastIdx)
                {
                    int charIdx = maxIdx - 1;
                    if (charIdx < _charSet.Length)
                    {
                        sb.Append(_charSet[charIdx]);
                    }
                }
                lastIdx = maxIdx;
            }

            return sb.ToString();
        }

        public void Dispose() => _recSession?.Dispose();
    }
}