using DAL.Data;
using DAL.DTOs;
using DAL.Entities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BLL.Services
{
    public class LicensePlateService : ILicensePlateService
    {
        private readonly OnnxInferenceEngine _engine;
        private readonly PaddleOcrService _ocr;
        private readonly ILogger<LicensePlateService> _logger;

        private const float ConfidenceThreshold = 0.25f;
        private const float IouThreshold = 0.45f;
        private const int ModelInputSize = 640;
        //LONG Plate
        private const float FrontExpandLeft = 0.0f;
        private const float FrontExpandRight = 0.0f;
        private const float FrontExpandTop = 0.0f;
        private const float FrontExpandBot = 0.0f;
        //Short Plate
        private const float BackExpandLeft = 0.0f;
        private const float BackExpandRight = 0.0f;
        private const float BackExpandTop = 0.0f;
        private const float BackExpandBot = 0.0f;

        public LicensePlateService(OnnxInferenceEngine engine, PaddleOcrService ocr, ILogger<LicensePlateService> logger)
        {
            _engine = engine;
            _ocr = ocr;
            _logger = logger;
        }

        // ─── Dual Camera Entry Point ────────────────────────────────────────
        public async Task<DualPlateResult> DetectDualPlateAsync(byte[]? frontImageBytes, byte[]? backImageBytes)
        {
            // Run both cameras in parallel
            var frontTask = frontImageBytes != null
                ? DetectSingleAsync(frontImageBytes, PlatePosition.Front)
                : Task.FromResult<SinglePlateResult?>(null);

            var backTask = backImageBytes != null
                ? DetectSingleAsync(backImageBytes, PlatePosition.Back)
                : Task.FromResult<SinglePlateResult?>(null);

            await Task.WhenAll(frontTask, backTask);

            var front = await frontTask;
            var back = await backTask;

            return ReconcileResults(front, back);
        }

        // ─── Single Camera Detection ────────────────────────────────────────
        private async Task<SinglePlateResult?> DetectSingleAsync(byte[] imageBytes, PlatePosition position)
        {
            var boxes = GetFilteredBoxes(imageBytes);
            if (!boxes.Any())
            {
                // Fallback: If no YOLO box detected (e.g. pre-cropped image uploaded), attempt direct OCR
                var fallbackText = await _ocr.ExtractTextAsync(imageBytes, "SHORT", position.ToString() + "_Fallback");
                if (!string.IsNullOrEmpty(fallbackText) && fallbackText.Count(char.IsLetterOrDigit) >= 4)
                {
                    return new SinglePlateResult
                    {
                        Detected = true,
                        PlateText = fallbackText,
                        Confidence = 0.75f,
                        Position = position,
                        PlateType = "SHORT"
                    };
                }
                return new SinglePlateResult
                {
                    Detected = false,
                    Position = position
                };
            }

            var bestBox = boxes.OrderByDescending(b => b.Confidence).First();
            var cropped = CropRegion(imageBytes, bestBox, position);
            var plateType = DetectPlateType(cropped, position);
            var plateText = await _ocr.ExtractTextAsync(cropped, plateType, position.ToString());

            return new SinglePlateResult
            {
                Detected = true,
                PlateText = plateText,
                Confidence = bestBox.Confidence,
                Position = position,
                PlateType = plateType
            };
        }

        // ─── Plate Type Detection ───────────────────────────────────────────
        private string DetectPlateType(byte[] croppedBytes, PlatePosition position = PlatePosition.Back)
        {
            // Note: Front camera always shoots LONG plate
            // Back camera always shoots SHORT plate
            // For special cars with 2 LONG plates, both cameras get LONG
            // — handle via ratio as fallback

            using var bitmap = SKBitmap.Decode(croppedBytes);
            float ratio = (float)bitmap.Width / bitmap.Height;

            _logger.LogInformation(
                "[{Pos}] Plate ratio: {Ratio:F2} ({W}x{H})",
                position, ratio, bitmap.Width, bitmap.Height);

            // Use position as primary signal
            if (position == PlatePosition.Front)
            {
                _logger.LogInformation("[Front] → LONG (camera position rule)");
                return "LONG";
            }

            // Back — use ratio to distinguish SHORT vs LONG
            var type = ratio >= 3.5f ? "LONG" : "SHORT";
            _logger.LogInformation("[Back] ratio={R:F2} → {T}", ratio, type);
            return type;
        }

        // ─── Reconcile Front + Back Results ────────────────────────────────
        private DualPlateResult ReconcileResults(SinglePlateResult? front, SinglePlateResult? back)
        {
            bool frontOk = front?.Detected == true
                           && !string.IsNullOrEmpty(front.PlateText);
            bool backOk = back?.Detected == true
                           && !string.IsNullOrEmpty(back.PlateText);

            // Both detected — compare and pick best
            if (frontOk && backOk)
            {
                // Normalize for comparison (remove dots, spaces)
                var frontNorm = Normalize(front!.PlateText);
                var backNorm = Normalize(back!.PlateText);

                // If they match — high confidence result
                if (frontNorm == backNorm)
                    return new DualPlateResult
                    {
                        Detected = true,
                        FinalPlateText = front.PlateText,
                        ConfirmedBy = "BOTH",
                        Front = front,
                        Back = back
                    };

                // They don't match — prefer back plate
                // (back plate is usually cleaner / less obstructed)
                // But pick whichever has higher YOLO confidence
                var better = front.Confidence >= back.Confidence ? front : back;
                return new DualPlateResult
                {
                    Detected = true,
                    FinalPlateText = better.PlateText,
                    ConfirmedBy = better.Position.ToString(),
                    Front = front,
                    Back = back
                };
            }

            // Only front detected
            if (frontOk)
                return new DualPlateResult
                {
                    Detected = true,
                    FinalPlateText = front!.PlateText,
                    ConfirmedBy = "FRONT",
                    Front = front,
                    Back = back
                };

            // Only back detected
            if (backOk)
                return new DualPlateResult
                {
                    Detected = true,
                    FinalPlateText = back!.PlateText,
                    ConfirmedBy = "BACK",
                    Front = front,
                    Back = back
                };

            // Neither detected
            return new DualPlateResult
            {
                Detected = false,
                Front = front,
                Back = back
            };
        }

        private string Normalize(string plateText) => new string(plateText.ToUpper()
                                                                          .Where(char.IsLetterOrDigit)
                                                                          .ToArray());

        // ─── Existing Single Image API (unchanged) ──────────────────────────
        public async Task<LicensePlateResult> DetectPlateAsync(byte[] imageBytes)
        {
            var boxes = GetFilteredBoxes(imageBytes);
            if (!boxes.Any())
            {
                // Fallback: If no YOLO box detected (e.g. pre-cropped image uploaded), attempt direct OCR
                var fallbackText = await _ocr.ExtractTextAsync(imageBytes, "SHORT", "Single_Fallback");
                if (!string.IsNullOrEmpty(fallbackText) && fallbackText.Count(char.IsLetterOrDigit) >= 4)
                {
                    return new LicensePlateResult
                    {
                        Detected = true,
                        PlateText = fallbackText,
                        Confidence = 0.75f,
                        Boxes = new List<DetectionBox>()
                    };
                }
                return new LicensePlateResult { Detected = false };
            }

            var bestBox = boxes.OrderByDescending(b => b.Confidence).First();
            var cropped = CropRegion(imageBytes, bestBox, PlatePosition.Back);
            var plateType = DetectPlateType(cropped, PlatePosition.Back);
            var plateText = await _ocr.ExtractTextAsync(cropped, plateType, "Single");

            return new LicensePlateResult
            {
                Detected = true,
                PlateText = plateText,
                Confidence = bestBox.Confidence,
                Boxes = boxes
            };
        }

        // ─── Shared Helpers ─────────────────────────────────────────────────
        private List<DetectionBox> GetFilteredBoxes(byte[] imageBytes)
        {
            var rawOutput = _engine.RunInference(imageBytes);
            var boxes = ParseYoloOutput(rawOutput);
            return ApplyNMS(boxes, IouThreshold);
        }

        private List<DetectionBox> ParseYoloOutput(float[] output)
        {
            var boxes = new List<DetectionBox>();
            int numDetections = 8400;

            for (int i = 0; i < numDetections; i++)
            {
                float conf = output[4 * numDetections + i];
                if (conf < ConfidenceThreshold) continue;

                float cx = output[0 * numDetections + i];
                float cy = output[1 * numDetections + i];
                float w = output[2 * numDetections + i];
                float h = output[3 * numDetections + i];

                boxes.Add(new DetectionBox
                {
                    X1 = (cx - w / 2) / ModelInputSize,
                    Y1 = (cy - h / 2) / ModelInputSize,
                    X2 = (cx + w / 2) / ModelInputSize,
                    Y2 = (cy + h / 2) / ModelInputSize,
                    Confidence = conf
                });
            }
            return boxes;
        }

        private List<DetectionBox> ApplyNMS(List<DetectionBox> boxes, float iouThreshold)
        {
            var sorted = boxes.OrderByDescending(b => b.Confidence).ToList();
            var kept = new List<DetectionBox>();

            while (sorted.Any())
            {
                var best = sorted[0];
                kept.Add(best);
                sorted.RemoveAt(0);
                sorted.RemoveAll(b => ComputeIoU(best, b) > iouThreshold);
            }
            return kept;
        }

        private float ComputeIoU(DetectionBox a, DetectionBox b)
        {
            float ix1 = Math.Max(a.X1, b.X1), iy1 = Math.Max(a.Y1, b.Y1);
            float ix2 = Math.Min(a.X2, b.X2), iy2 = Math.Min(a.Y2, b.Y2);
            float inter = Math.Max(0, ix2 - ix1) * Math.Max(0, iy2 - iy1);
            float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
            return inter / (areaA + areaB - inter);
        }

        private byte[] CropRegion(byte[] imageBytes, DetectionBox box, PlatePosition position = PlatePosition.Back)
        {
            using var bitmap = SKBitmap.Decode(imageBytes);

            _logger.LogInformation(
                "[{Pos}] Raw YOLO box — X1:{X1:F3} Y1:{Y1:F3} X2:{X2:F3} Y2:{Y2:F3}",
                position, box.X1, box.Y1, box.X2, box.Y2);

            float bw = box.X2 - box.X1;
            float bh = box.Y2 - box.Y1;

            float expandLeft, expandRight, expandTop, expandBot;

            if (position == PlatePosition.Front)
            {
                expandLeft = bw * FrontExpandLeft;
                expandRight = bw * FrontExpandRight;
                expandTop = bh * FrontExpandTop;
                expandBot = bh * FrontExpandBot;
            }
            else
            {
                expandLeft = bw * BackExpandLeft;
                expandRight = bw * BackExpandRight;
                expandTop = bh * BackExpandTop;
                expandBot = bh * BackExpandBot;
            }

            float x1 = Math.Max(0f, box.X1 - expandLeft);
            float y1 = Math.Max(0f, box.Y1 - expandTop);
            float x2 = Math.Min(1f, box.X2 + expandRight);
            float y2 = Math.Min(1f, box.Y2 + expandBot);

            int x = (int)(x1 * bitmap.Width);
            int y = (int)(y1 * bitmap.Height);
            int w = (int)((x2 - x1) * bitmap.Width);
            int h = (int)((y2 - y1) * bitmap.Height);

            w = Math.Min(w, bitmap.Width - x);
            h = Math.Min(h, bitmap.Height - y);

            _logger.LogInformation(
                "[{Pos}] Expanded crop — x:{X} y:{Y} w:{W} h:{H}",
                position, x, y, w, h);

            using var cropped = new SKBitmap(w, h);
            bitmap.ExtractSubset(cropped, new SKRectI(x, y, x + w, y + h));

            using var ms = new MemoryStream();
            cropped.Encode(ms, SKEncodedImageFormat.Png, 100);
            var bytes = ms.ToArray();

            File.WriteAllBytes(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"debug_yolo_crop_{position}.png"), bytes);

            return bytes;
        }
    }
}