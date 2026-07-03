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

                    using var rawBmp = SKBitmap.Decode(imageBytes);
                    if (rawBmp == null) return string.Empty;

                    var oneLine = RecognizeBestTextLine(rawBmp, position + "_oneline");
                    int cleanLen = oneLine.text.Count(char.IsLetterOrDigit);

                    float imgRatio = (float)rawBmp.Width / Math.Max(1, rawBmp.Height);
                    bool isTwoLineCandidate = plateType == "SHORT" || imgRatio <= 2.3f || IsTwoLinePlate(rawBmp);

                    if (!isTwoLineCandidate)
                    {
                        _logger.LogInformation("[{Pos}] Standard 1-line layout: '{Text}' (Conf: {Conf:F3})", position, oneLine.text, oneLine.conf);
                        return PostProcessPlateText(oneLine.text);
                    }

                    _logger.LogInformation("[{Pos}] Evaluating 2-line layout...", position);
                    return PostProcessPlateText(ReadTwoLinePlate(rawBmp, position, oneLine));
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

        // ── Two-line plate (biển ngắn) ───────────────────────────────────────
        private string ReadTwoLinePlate(SKBitmap rawBmp, string position, (string text, float conf) oneLineResult)
        {
            int scale = Math.Max(3, 300 / Math.Max(rawBmp.Width, 1));
            int W = rawBmp.Width * scale;
            int H = rawBmp.Height * scale;

            var samplingOptions = new SKSamplingOptions(SKCubicResampler.Mitchell);
            using var upscaled = rawBmp.Resize(new SKImageInfo(W, H), samplingOptions);

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

            var line1 = RecognizeBestTextLine(topBmp, position + "_line1");
            var line2 = RecognizeBestTextLine(botBmp, position + "_line2");

            _logger.LogInformation("[{Pos}] Line 1: '{A}' (Conf: {C1:F3})", position, line1.text, line1.conf);
            _logger.LogInformation("[{Pos}] Line 2: '{B}' (Conf: {C2:F3})", position, line2.text, line2.conf);

            if (line1.conf < 0.55f || line2.conf < 0.55f)
            {
                _logger.LogInformation("[{Pos}] Low confidence on 2-line split (L1:{C1:F2}, L2:{C2:F2}), preferring 1-line", position, line1.conf, line2.conf);
                return oneLineResult.text;
            }

            string l2Text = line2.text;
            if (l2Text.Count(char.IsDigit) > 5)
            {
                if (l2Text.StartsWith("1") || l2Text.StartsWith("I") || l2Text.StartsWith("l"))
                    l2Text = l2Text.Substring(1);
                else if (l2Text.EndsWith("1") || l2Text.EndsWith("I") || l2Text.EndsWith("l"))
                    l2Text = l2Text.Substring(0, l2Text.Length - 1);
            }

            float oneScore = oneLineResult.conf + oneLineResult.text.Count(char.IsLetterOrDigit) * 0.15f;
            float twoScore = ((line1.conf + line2.conf) / 2f) + (line1.text.Count(char.IsLetterOrDigit) + l2Text.Count(char.IsLetterOrDigit)) * 0.15f;

            if (oneScore >= twoScore || line1.text.Count(char.IsLetterOrDigit) < 2 || l2Text.Count(char.IsLetterOrDigit) < 2)
            {
                _logger.LogInformation("[{Pos}] 1-line score ({S1:F2}) >= 2-line score ({S2:F2}), preferring 1-line", position, oneScore, twoScore);
                return oneLineResult.text;
            }

            return (line1.text + l2Text).ToUpper();
        }

        // ── Multi-Candidate Ensemble Recognition ─────────────────────────────
        private (string text, float conf) RecognizeBestTextLine(SKBitmap sourceBmp, string debugName)
        {
            var candidates = new List<(string text, float conf, float score, SKBitmap bmp)>();

            float[] ths  = { 0.00f, 0.25f, 0.35f, 0.25f, 0.25f, 0.35f };
            float[] tops = { 0.00f, 0.00f, 0.00f, 0.12f, 0.20f, 0.20f };
            float[] bots = { 1.00f, 1.00f, 1.00f, 0.95f, 0.90f, 0.90f };

            for (int i = 0; i < ths.Length; i++)
            {
                var c = ths[i] == 0.0f ? sourceBmp.Copy() : AutoCropY(sourceBmp, ths[i], tops[i], bots[i]);
                if (c.Width >= 5 && c.Height >= 5)
                {
                    var (text, conf) = RunOnnxRecognitionWithConf(c);
                    int cleanLen = text.Count(char.IsLetterOrDigit);
                    float cropPenalty = (tops[i] >= 0.10f) ? 0.12f : 0.0f;
                    float score = conf - cropPenalty + cleanLen * 0.15f;
                    candidates.Add((text, conf, score, c));
                }
                else
                {
                    c.Dispose();
                }
            }

            if (!candidates.Any()) return (string.Empty, 0f);

            var best = candidates.OrderByDescending(c => c.score).First();

            try
            {
                using var ms = new MemoryStream();
                best.bmp.Encode(ms, SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"debug_{debugName}.png"),
                    ms.ToArray());
            }
            catch { /* Ignore debug save errors */ }

            foreach (var cand in candidates) cand.bmp.Dispose();

            return (best.text, best.conf);
        }

        private SKBitmap AutoCropY(SKBitmap bmp, float thMult, float topSkipRatio, float botLimitRatio)
        {
            int W = bmp.Width;
            int H = bmp.Height;
            int startY = (int)(H * topSkipRatio);
            int endY = (int)(H * botLimitRatio);
            float[] rowEdges = new float[H];
            float maxRowEdge = 0f;
            for (int y = startY; y < endY; y++)
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

            float thresholdY = maxRowEdge * thMult;
            int minY = startY;
            while (minY < endY - 1 && rowEdges[minY] < thresholdY) minY++;

            int maxY = endY - 1;
            while (maxY > minY && rowEdges[maxY] < thresholdY) maxY--;

            int padY = Math.Max(1, (maxY - minY) / 30);
            minY = Math.Max(startY, minY - padY);
            maxY = Math.Min(endY - 1, maxY + padY);

            if (maxY <= minY + 2) return bmp.Copy();

            var cropped = new SKBitmap(W, maxY - minY + 1);
            bmp.ExtractSubset(cropped, new SKRectI(0, minY, W, maxY + 1));
            return cropped;
        }

        private string PostProcessPlateText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string upper = text.ToUpper().Trim();
            if (upper.Length >= 4 && upper.EndsWith("I"))
            {
                if (char.IsDigit(upper[upper.Length - 2]) && char.IsDigit(upper[upper.Length - 3]))
                {
                    upper = upper.Substring(0, upper.Length - 1) + "1";
                }
            }
            return upper;
        }

        // ── Recognition ──────────────────────────────────────────────────────
        private (string text, float conf) RunOnnxRecognitionWithConf(SKBitmap bitmap)
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

            return DecodeCtcWithConf(output);
        }

        // ── CTC Decoder ──────────────────────────────────────────────────────
        private (string text, float conf) DecodeCtcWithConf(Tensor<float> output)
        {
            int T = output.Dimensions[1];
            int numClasses = output.Dimensions[2];

            var sb = new System.Text.StringBuilder();
            float totalConf = 0f;
            int count = 0;
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
                        totalConf += maxVal;
                        count++;
                    }
                }
                lastIdx = maxIdx;
            }

            float avgConf = count > 0 ? totalConf / count : 0f;
            return (sb.ToString(), avgConf);
        }

        public void Dispose() => _recSession?.Dispose();
    }
}