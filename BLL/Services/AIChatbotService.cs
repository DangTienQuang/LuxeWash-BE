using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Constants;
using BLL.DTOs;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services
{
    public class AIChatbotService : IAIChatbotService
    {
        private readonly AutoWashDbContext _context;
        private readonly IAIModerationService _moderation;
        private readonly ILLMService _llm;
        private readonly IAIIntentService _intentService;

        public AIChatbotService(
            AutoWashDbContext context,
            IAIModerationService moderation,
            ILLMService llm,
            IAIIntentService intentService)
        {
            _context = context;
            _moderation = moderation;
            _llm = llm;
            _intentService = intentService;
        }

        public async Task<AIChatResponseDTO> ChatAsync(
            int userId,
            AIChatRequestDTO request)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.Message))
            {
                throw new Exception(
                    "Tin nhắn không được để trống.");
            }

            var originalMessage = request.Message.Trim();

            // =========================
            // MODERATION
            // =========================
            if (_moderation.IsBlocked(originalMessage))
            {
                var reason =
                    _moderation.GetBlockedReason(originalMessage);

                await LogConversationAsync(
                    userId,
                    originalMessage,
                    reason ?? "Blocked",
                    true);

                throw new Exception(
                    reason ?? "Tin nhắn không hợp lệ.");
            }

            var msg = originalMessage.ToLower();

            // =========================
            // FAST RULE-BASED INTENT
            // =========================
            string? intent = null;

            if (msg.Contains("điểm")
                || msg.Contains("point"))
            {
                intent = AIIntent.CheckPoints;
            }
            else if (msg.Contains("hạng")
                || msg.Contains("tier")
                || msg.Contains("gold")
                || msg.Contains("silver")
                || msg.Contains("vip"))
            {
                intent = AIIntent.CheckTier;
            }
            else if (msg.Contains("lần cuối")
                || msg.Contains("ghé")
                || msg.Contains("visit"))
            {
                intent = AIIntent.LastVisit;
            }
            else if (msg.Contains("giới thiệu")
                || msg.Contains("referral")
                || msg.Contains("mã giới thiệu"))
            {
                intent = AIIntent.Referral;
            }

            // =========================
            // AI FALLBACK INTENT
            // =========================
            if (string.IsNullOrWhiteSpace(intent))
            {
                intent = await _intentService
                    .DetectIntentAsync(originalMessage);
            }

            // =========================
            // MEMORY
            // =========================
            var memory =
                await BuildConversationMemoryAsync(userId);

            // =========================
            // CUSTOMER CONTEXT
            // =========================
            var profile = await _context.CustomerProfiles
                .Include(x => x.Tier)
                .Include(x => x.User)
                .FirstOrDefaultAsync(
                    x => x.UserId == userId);

            var vehicles = await _context.Vehicles
                .Where(x => x.UserId == userId)
                .ToListAsync();

            var vehicleText = vehicles.Any()
                ? string.Join(
                    ", ",
                    vehicles.Select(x => x.LicensePlate))
                : "Chưa có xe";

            string reply =
                "Xin lỗi, tôi chưa hiểu câu hỏi của bạn.";

            // =========================
            // SYSTEM PROMPT
            // =========================
            var systemPrompt = """
                Bạn là trợ lý AI của AutoWashPro.

                Nguyên tắc:
                - Luôn trả lời bằng tiếng Việt.
                - Trả lời thân thiện, ngắn gọn, chuyên nghiệp.
                - Không trả lời chính trị, tôn giáo, bạo lực.
                - Không trả lời nội dung nhạy cảm.
                - Không tự bịa thông tin.
                - Không trả lời ngoài lĩnh vực AutoWashPro.
                - Có thể dùng emoji nhẹ nhàng.
                """;

            // =========================
            // HANDLE INTENT
            // =========================
            switch (intent)
            {
                // =========================
                // CHECK POINTS
                // =========================
                case AIIntent.CheckPoints:
                    {
                        var spendablePoints = profile?.TotalPoint ?? 0;
                        var promotionPoints = profile?.PromotionPoint ?? 0;

                        try
                        {
                            var userPrompt = $"""
                                Lịch sử hội thoại:

                                {memory}

                                ====================

                                Thông tin khách hàng:

                                Tên:
                                {profile?.FullName}

                                Hạng:
                                {profile?.Tier?.TierName}

                                Xe:
                                {vehicleText}

                                Điểm khả dụng (TotalPoint):
                                {spendablePoints}

                                Điểm thăng hạng (PromotionPoint):
                                {promotionPoints}

                                Hãy trả lời tự nhiên và thân thiện.
                                """;

                            reply = await GenerateSafeReplyAsync(
                                systemPrompt,
                                userPrompt,
                                $"Bạn có {spendablePoints} điểm khả dụng và {promotionPoints} điểm thăng hạng.");
                        }
                        catch
                        {
                            reply =
                                $"Bạn có {spendablePoints} điểm khả dụng và {promotionPoints} điểm thăng hạng.";
                        }

                        break;
                    }

                // =========================
                // CHECK TIER
                // =========================
                case AIIntent.CheckTier:
                    {
                        if (profile == null)
                        {
                            reply =
                                "Không tìm thấy hồ sơ khách hàng.";

                            break;
                        }

                        var promotionPoints = profile.PromotionPoint;

                        var nextTier = await _context.Tiers
                            .Where(x =>
                                x.MinAccumulatedPoints >
                                profile.Tier.MinAccumulatedPoints)
                            .OrderBy(x => x.MinAccumulatedPoints)
                            .FirstOrDefaultAsync();

                        string upgradeMessage;

                        if (nextTier == null)
                        {
                            upgradeMessage =
                                "Bạn đang ở hạng cao nhất.";
                        }
                        else
                        {
                            int needed =
                                nextTier.MinAccumulatedPoints
                                - promotionPoints;

                            upgradeMessage =
                                $"Bạn cần thêm {needed} điểm thăng hạng để lên {nextTier.TierName}.";
                        }

                        try
                        {
                            var userPrompt = $"""
                                Lịch sử hội thoại:

                                {memory}

                                ====================

                                Thông tin khách hàng:

                                Tên:
                                {profile.FullName}

                                Hạng hiện tại:
                                {profile.Tier.TierName}

                                Điểm thăng hạng (PromotionPoint):
                                {promotionPoints}

                                Xe:
                                {vehicleText}

                                {upgradeMessage}

                                Hãy phản hồi tự nhiên và chuyên nghiệp.
                                """;

                            reply = await GenerateSafeReplyAsync(
                                systemPrompt,
                                userPrompt,
                                $"Bạn hiện đang ở hạng {profile.Tier.TierName}. {upgradeMessage}");
                        }
                        catch
                        {
                            reply =
                                $"Bạn hiện đang ở hạng {profile.Tier.TierName}. " +
                                upgradeMessage;
                        }

                        break;
                    }

                // =========================
                // LAST VISIT
                // =========================
                case AIIntent.LastVisit:
                    {
                        if (profile?.LastVisitDate == null)
                        {
                            reply =
                                "Bạn chưa có lịch sử sử dụng dịch vụ.";
                        }
                        else
                        {
                            try
                            {
                                var userPrompt = $"""
                                    Lịch sử hội thoại:

                                    {memory}

                                    ====================

                                    Khách hàng:
                                    {profile.FullName}

                                    Hạng:
                                    {profile.Tier?.TierName}

                                    Xe:
                                    {vehicleText}

                                    Lần sử dụng dịch vụ gần nhất:
                                    {profile.LastVisitDate:dd/MM/yyyy}

                                    Hãy phản hồi tự nhiên.
                                    """;

                                reply = await GenerateSafeReplyAsync(
                                    systemPrompt,
                                    userPrompt,
                                    $"Lần sử dụng dịch vụ gần nhất của bạn là ngày {profile.LastVisitDate:dd/MM/yyyy}.");
                            }
                            catch
                            {
                                reply =
                                    $"Lần sử dụng dịch vụ gần nhất của bạn là ngày " +
                                    $"{profile.LastVisitDate:dd/MM/yyyy}.";
                            }
                        }

                        break;
                    }

                // =========================
                // REFERRAL
                // =========================
                case AIIntent.Referral:
                    {
                        if (profile == null)
                        {
                            reply =
                                "Không tìm thấy thông tin khách hàng.";
                        }
                        else if (string.IsNullOrWhiteSpace(
                            profile.ReferralCode))
                        {
                            reply =
                                "Bạn hiện chưa có mã giới thiệu.";
                        }
                        else
                        {
                            try
                            {
                                var userPrompt = $"""
                                    Lịch sử hội thoại:

                                    {memory}

                                    ====================

                                    Khách hàng:
                                    {profile.FullName}

                                    Hạng:
                                    {profile.Tier?.TierName}

                                    Mã giới thiệu:
                                    {profile.ReferralCode}

                                    Hãy trả lời tự nhiên và thân thiện.
                                    """;

                                reply = await GenerateSafeReplyAsync(
                                    systemPrompt,
                                    userPrompt,
                                    $"Mã giới thiệu của bạn là: {profile.ReferralCode}");
                            }
                            catch
                            {
                                reply =
                                    $"Mã giới thiệu của bạn là: {profile.ReferralCode}";
                            }
                        }

                        break;
                    }

                // =========================
                // UNKNOWN
                // =========================
                default:
                    {
                        try
                        {
                            var userPrompt = $"""
                                Lịch sử hội thoại:

                                {memory}

                                ====================

                                Khách hàng hỏi:
                                "{originalMessage}"

                                Hãy lịch sự nói rằng hệ thống hiện chỉ hỗ trợ:
                                - điểm thưởng
                                - hạng thành viên
                                - lịch sử sử dụng dịch vụ
                                - mã giới thiệu
                                - khuyến mãi
                                """;

                            reply = await GenerateSafeReplyAsync(
                                systemPrompt,
                                userPrompt,
                                "Xin lỗi, tôi hiện chỉ hỗ trợ các câu hỏi về dịch vụ AutoWashPro.");
                        }
                        catch
                        {
                            reply =
                                "Xin lỗi, tôi hiện chỉ hỗ trợ các câu hỏi về dịch vụ AutoWashPro.";
                        }

                        break;
                    }
            }

            // =========================
            // LOG CONVERSATION
            // =========================
            await LogConversationAsync(
                userId,
                originalMessage,
                reply,
                false);

            return new AIChatResponseDTO
            {
                Intent = intent ?? AIIntent.Unknown,
                Reply = reply
            };
        }

        public async Task<string> GetRecommendationAsync(
            int userId)
        {
            var profile = await _context.CustomerProfiles
                .Include(x => x.Tier)
                .FirstOrDefaultAsync(
                    x => x.UserId == userId);

            var vehicles = await _context.Vehicles
                .Where(x => x.UserId == userId)
                .ToListAsync();

            var spendablePoints = profile?.TotalPoint ?? 0;
            var promotionPoints = profile?.PromotionPoint ?? 0;

            string recommendation;

            if (profile == null)
            {
                recommendation =
                    "Hãy trải nghiệm dịch vụ rửa xe Premium.";
            }
            else
            {
                recommendation =
                    "Nâng cấp lên hạng Gold để nhận thêm ưu đãi.";

                if (profile.LastVisitDate.HasValue)
                {
                    var daysSinceLastVisit =
                        (DateTime.UtcNow
                         - profile.LastVisitDate.Value).Days;

                    if (daysSinceLastVisit > 30)
                    {
                        recommendation =
                            "Bạn đã lâu chưa quay lại. Tuần này có voucher giảm 20% cho lần rửa tiếp theo.";
                    }
                }

                if (profile.Tier.TierName
                    .ToLower()
                    .Contains("gold"))
                {
                    recommendation =
                        "Khách hàng Gold hiện được miễn phí phủ bóng nhanh.";
                }
            }

            try
            {
                var vehicleText = vehicles.Any()
                    ? string.Join(
                        ", ",
                        vehicles.Select(x => x.LicensePlate))
                    : "Không có dữ liệu xe";

                var systemPrompt = """
                    Bạn là AI marketing assistant của AutoWashPro.

                    Hãy viết nội dung:
                    - ngắn gọn
                    - hấp dẫn
                    - tự nhiên
                    - bằng tiếng Việt
                    - tăng khả năng khách quay lại
                    - có thể dùng emoji nhẹ
                    """;

                var userPrompt = $"""
                    Thông tin khách hàng:

                    Hạng:
                    {profile?.Tier?.TierName}

                    Điểm khả dụng:
                    {spendablePoints}

                    Điểm thăng hạng:
                    {promotionPoints}

                    Xe:
                    {vehicleText}

                    Lần cuối sử dụng:
                    {profile?.LastVisitDate:dd/MM/yyyy}

                    Nội dung ưu đãi:
                    {recommendation}

                    Hãy viết lại hấp dẫn hơn.
                    """;

                return await GenerateSafeReplyAsync(
                    systemPrompt,
                    userPrompt,
                    recommendation);
            }
            catch
            {
                return recommendation;
            }
        }

        // =========================
        // SAFE AI GENERATION
        // =========================
        private async Task<string> GenerateSafeReplyAsync(
            string systemPrompt,
            string userPrompt,
            string fallback)
        {
            try
            {
                var aiTask = _llm.GenerateReplyAsync(
                    systemPrompt,
                    userPrompt);

                var completedTask = await Task.WhenAny(
                    aiTask,
                    Task.Delay(5000));

                if (completedTask != aiTask)
                {
                    return fallback;
                }

                var aiReply = await aiTask;

                if (!IsSafeAIResponse(aiReply))
                {
                    return fallback;
                }

                return aiReply;
            }
            catch
            {
                return fallback;
            }
        }

        // =========================
        // AI SAFETY CHECK
        // =========================
        private bool IsSafeAIResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            var bannedWords = new[]
            {
                "chính trị",
                "18+",
                "sex",
                "ma túy",
                "bạo lực"
            };

            return !bannedWords.Any(x =>
                response.ToLower().Contains(x));
        }

        // =========================
        // LOG CONVERSATION
        // =========================
        private async Task LogConversationAsync(
            int userId,
            string message,
            string response,
            bool blocked)
        {
            try
            {
                var log = new AIConversationLog
                {
                    UserId = userId,
                    Message = message,
                    Response = response,
                    Blocked = blocked,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AIConversationLogs.Add(log);

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Prevent crashing if logging fails
            }
        }

        // =========================
        // RECENT MEMORY
        // =========================
        private async Task<List<AIConversationLog>>
            GetRecentConversationAsync(int userId)
        {
            return await _context.AIConversationLogs
                .Where(x =>
                    x.UserId == userId &&
                    !x.Blocked)
                .OrderByDescending(x => x.CreatedAt)
                .Take(5)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        // =========================
        // BUILD MEMORY
        // =========================
        private async Task<string>
            BuildConversationMemoryAsync(int userId)
        {
            var history =
                await GetRecentConversationAsync(userId);

            if (!history.Any())
            {
                return "";
            }

            var lines = history.Select(x =>
                $"User: {x.Message}\nAI: {x.Response}");

            return string.Join("\n\n", lines);
        }
    }
}
