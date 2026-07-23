using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Constants;
using BLL.DTOs;
using BLL.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoWashPro.Tests.BLL
{
    public class AIChatbotServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IAIModerationService> _moderationMock;
        private readonly Mock<ILLMService> _llmMock;
        private readonly Mock<IAIIntentService> _intentServiceMock;
        private readonly AIChatbotService _sut;

        public AIChatbotServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _moderationMock = new Mock<IAIModerationService>();
            _llmMock = new Mock<ILLMService>();
            _intentServiceMock = new Mock<IAIIntentService>();

            _moderationMock.Setup(m => m.IsBlocked(It.IsAny<string>())).Returns(false);
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("Đây là phản hồi AI mẫu.");
            _intentServiceMock.Setup(i => i.DetectIntentAsync(It.IsAny<string>()))
                .ReturnsAsync(AIIntent.Unknown);

            _sut = new AIChatbotService(_dbContext, _moderationMock.Object, _llmMock.Object, _intentServiceMock.Object);
        }

        [Fact]
        public async Task ChatAsync_NullRequest_ShouldThrowException()
        {
            await Assert.ThrowsAsync<Exception>(() => _sut.ChatAsync(1, null));
        }

        [Fact]
        public async Task ChatAsync_EmptyMessage_ShouldThrowException()
        {
            var request = new AIChatRequestDTO { Message = "   " };
            await Assert.ThrowsAsync<Exception>(() => _sut.ChatAsync(1, request));
        }

        [Fact]
        public async Task ChatAsync_BlockedMessage_WithReason_ShouldThrowAndLogBlocked()
        {
            _moderationMock.Setup(m => m.IsBlocked("bad message")).Returns(true);
            _moderationMock.Setup(m => m.GetBlockedReason("bad message")).Returns("Nội dung không phù hợp");

            var request = new AIChatRequestDTO { Message = "bad message" };

            var ex = await Assert.ThrowsAsync<Exception>(() => _sut.ChatAsync(1, request));
            Assert.Equal("Nội dung không phù hợp", ex.Message);

            var log = await _dbContext.AIConversationLogs.FirstOrDefaultAsync(l => l.UserId == 1);
            Assert.NotNull(log);
            Assert.True(log.Blocked);
        }

        [Fact]
        public async Task ChatAsync_BlockedMessage_NullReason_ShouldThrowGenericMessage()
        {
            _moderationMock.Setup(m => m.IsBlocked("bad message")).Returns(true);
            _moderationMock.Setup(m => m.GetBlockedReason("bad message")).Returns((string)null);

            var request = new AIChatRequestDTO { Message = "bad message" };

            var ex = await Assert.ThrowsAsync<Exception>(() => _sut.ChatAsync(1, request));
            Assert.Equal("Tin nhắn không hợp lệ.", ex.Message);
        }

        [Fact]
        public async Task ChatAsync_KeywordPoint_ShouldRouteToCheckPoints_WithoutCallingIntentService()
        {
            var request = new AIChatRequestDTO { Message = "Tôi có bao nhiêu điểm?" };

            var result = await _sut.ChatAsync(1, request);

            Assert.Equal(AIIntent.CheckPoints, result.Intent);
            _intentServiceMock.Verify(i => i.DetectIntentAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChatAsync_KeywordTier_ShouldRouteToCheckTier()
        {
            var request = new AIChatRequestDTO { Message = "Hạng của tôi là gì?" };
            var result = await _sut.ChatAsync(1, request);
            Assert.Equal(AIIntent.CheckTier, result.Intent);
        }

        [Fact]
        public async Task ChatAsync_KeywordVisit_ShouldRouteToLastVisit()
        {
            var request = new AIChatRequestDTO { Message = "Lần cuối tôi ghé là khi nào?" };
            var result = await _sut.ChatAsync(1, request);
            Assert.Equal(AIIntent.LastVisit, result.Intent);
        }

        [Fact]
        public async Task ChatAsync_KeywordReferral_ShouldRouteToReferral()
        {
            var request = new AIChatRequestDTO { Message = "Mã giới thiệu của tôi là gì?" };
            var result = await _sut.ChatAsync(1, request);
            Assert.Equal(AIIntent.Referral, result.Intent);
        }

        [Fact]
        public async Task ChatAsync_NoKeywordMatch_ShouldFallBackToIntentService()
        {
            _intentServiceMock.Setup(i => i.DetectIntentAsync(It.IsAny<string>())).ReturnsAsync(AIIntent.Unknown);

            var request = new AIChatRequestDTO { Message = "Xin chào bạn khỏe không" };
            var result = await _sut.ChatAsync(1, request);

            Assert.Equal(AIIntent.Unknown, result.Intent);
            _intentServiceMock.Verify(i => i.DetectIntentAsync("Xin chào bạn khỏe không"), Times.Once);
        }

        [Fact]
        public async Task ChatAsync_CheckPoints_ProfileNull_UsesZeroDefaults()
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("skip AI")); // force fallback path so we can check the raw 0-point text

            var request = new AIChatRequestDTO { Message = "điểm của tôi" };
            var result = await _sut.ChatAsync(999, request);

            Assert.Equal(AIIntent.CheckPoints, result.Intent);
            Assert.Contains("0 điểm", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_CheckPoints_AISucceeds_ReturnsAIReply()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000001", Email = "cp1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "A", TierId = tier.TierId, TotalPoint = 50, PromotionPoint = 10 });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("Bạn có 50 điểm nhé!");

            var request = new AIChatRequestDTO { Message = "điểm của tôi" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Equal("Bạn có 50 điểm nhé!", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_CheckPoints_AIThrows_FallsBackToPlainText()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000002", Email = "cp2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "B", TierId = tier.TierId, TotalPoint = 30, PromotionPoint = 5 });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("LLM down"));

            var request = new AIChatRequestDTO { Message = "điểm của tôi" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Contains("30 điểm khả dụng", result.Reply);
            Assert.Contains("5 điểm thăng hạng", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_CheckTier_ProfileNull_ReturnsNotFoundReply()
        {
            var request = new AIChatRequestDTO { Message = "hạng của tôi" };
            var result = await _sut.ChatAsync(999, request);

            Assert.Equal("Không tìm thấy hồ sơ khách hàng.", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_CheckTier_AtHighestTier_ShowsHighestTierMessage()
        {
            var tier = new Tier { TierName = "Diamond", PointMultiplier = 2.0, BookingWindowDays = 14, MinAccumulatedPoints = 1000 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000003", Email = "ct1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "C", TierId = tier.TierId, PromotionPoint = 1200 });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var request = new AIChatRequestDTO { Message = "hạng của tôi" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Contains("hạng cao nhất", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_CheckTier_NotHighest_ShowsPointsNeeded()
        {
            var lowTier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            var highTier = new Tier { TierName = "Gold", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.AddRange(lowTier, highTier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000004", Email = "ct2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "D", TierId = lowTier.TierId, PromotionPoint = 300 });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var request = new AIChatRequestDTO { Message = "hạng của tôi" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Contains("200 điểm", result.Reply); // 500 - 300
            Assert.Contains("Gold", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_LastVisit_NoDateOnRecord_ReturnsNoHistoryReply()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000005", Email = "lv1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "E", TierId = tier.TierId, LastVisitDate = null });
            await _dbContext.SaveChangesAsync();

            var request = new AIChatRequestDTO { Message = "lần cuối tôi ghé" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Equal("Bạn chưa có lịch sử sử dụng dịch vụ.", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_LastVisit_HasDate_AIThrows_FallsBackWithFormattedDate()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000006", Email = "lv2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var visitDate = new DateTime(2026, 5, 1);
            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "F", TierId = tier.TierId, LastVisitDate = visitDate });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var request = new AIChatRequestDTO { Message = "lần cuối tôi ghé" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Contains("01/05/2026", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_Referral_ProfileNull_ReturnsNotFoundReply()
        {
            var request = new AIChatRequestDTO { Message = "mã giới thiệu" };
            var result = await _sut.ChatAsync(999, request);

            Assert.Equal("Không tìm thấy thông tin khách hàng.", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_Referral_NoCode_ReturnsNoCodeReply()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000007", Email = "ref1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "G", TierId = tier.TierId, ReferralCode = null });
            await _dbContext.SaveChangesAsync();

            var request = new AIChatRequestDTO { Message = "mã giới thiệu" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Equal("Bạn hiện chưa có mã giới thiệu.", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_Referral_HasCode_AIThrows_FallsBackWithCode()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0970000008", Email = "ref2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "H", TierId = tier.TierId, ReferralCode = "REF123" });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var request = new AIChatRequestDTO { Message = "mã giới thiệu" };
            var result = await _sut.ChatAsync(user.UserId, request);

            Assert.Contains("REF123", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_UnknownIntent_AIThrows_ReturnsGenericFallback()
        {
            _intentServiceMock.Setup(i => i.DetectIntentAsync(It.IsAny<string>())).ReturnsAsync(AIIntent.Unknown);
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var request = new AIChatRequestDTO { Message = "random gibberish question" };
            var result = await _sut.ChatAsync(1, request);

            Assert.Equal(AIIntent.Unknown, result.Intent);
            Assert.Equal("Xin lỗi, tôi hiện chỉ hỗ trợ các câu hỏi về dịch vụ AutoWashPro.", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_UnknownIntent_AISucceeds_ReturnsAIReply()
        {
            _intentServiceMock.Setup(i => i.DetectIntentAsync(It.IsAny<string>())).ReturnsAsync(AIIntent.Unknown);
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("Tôi chỉ hỗ trợ các câu hỏi về AutoWashPro nhé!");

            var request = new AIChatRequestDTO { Message = "random gibberish question" };
            var result = await _sut.ChatAsync(1, request);

            Assert.Equal("Tôi chỉ hỗ trợ các câu hỏi về AutoWashPro nhé!", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_UnsafeAIResponse_ShouldUseFallbackInstead()
        {
            // AI returns a response containing a banned word — IsSafeAIResponse should reject it
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("Đây là nội dung chính trị nhạy cảm.");

            var request = new AIChatRequestDTO { Message = "điểm của tôi" };
            var result = await _sut.ChatAsync(999, request);

            // Should NOT contain the unsafe AI text — falls back to the plain "0 điểm" message instead
            Assert.DoesNotContain("chính trị", result.Reply);
            Assert.Contains("0 điểm", result.Reply);
        }

        [Fact]
        public async Task ChatAsync_SuccessfulConversation_ShouldLogWithBlockedFalse()
        {
            var request = new AIChatRequestDTO { Message = "điểm của tôi" };
            await _sut.ChatAsync(1, request);

            var log = await _dbContext.AIConversationLogs
                .Where(l => l.UserId == 1)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            Assert.NotNull(log);
            Assert.False(log.Blocked);
            Assert.Equal("điểm của tôi", log.Message);
        }

        [Fact]
        public async Task GetRecommendationAsync_ProfileNull_ReturnsDefaultPremiumMessage()
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var result = await _sut.GetRecommendationAsync(999);

            Assert.Equal("Hãy trải nghiệm dịch vụ rửa xe Premium.", result);
        }

        [Fact]
        public async Task GetRecommendationAsync_NoLastVisitDate_ReturnsDefaultUpgradeMessage()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0980000001", Email = "rec1@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "I", TierId = tier.TierId, LastVisitDate = null });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var result = await _sut.GetRecommendationAsync(user.UserId);

            Assert.Equal("Nâng cấp lên hạng Gold để nhận thêm ưu đãi.", result);
        }

        [Fact]
        public async Task GetRecommendationAsync_LastVisitOver30DaysAgo_ReturnsComeBackMessage()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0980000002", Email = "rec2@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "J", TierId = tier.TierId, LastVisitDate = DateTime.UtcNow.AddDays(-45) });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var result = await _sut.GetRecommendationAsync(user.UserId);

            Assert.Contains("voucher giảm 20%", result);
        }

        [Fact]
        public async Task GetRecommendationAsync_LastVisitWithin30Days_KeepsDefaultUpgradeMessage()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0980000003", Email = "rec3@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "K", TierId = tier.TierId, LastVisitDate = DateTime.UtcNow.AddDays(-10) });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var result = await _sut.GetRecommendationAsync(user.UserId);

            Assert.Equal("Nâng cấp lên hạng Gold để nhận thêm ưu đãi.", result);
        }

        [Fact]
        public async Task GetRecommendationAsync_GoldTier_OverridesToGoldMessage_EvenIfLongAbsent()
        {
            // Gold tier check happens AFTER the 30-day check and should override it
            var tier = new Tier { TierName = "Gold Member", PointMultiplier = 1.5, BookingWindowDays = 10, MinAccumulatedPoints = 500 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0980000004", Email = "rec4@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "L", TierId = tier.TierId, LastVisitDate = DateTime.UtcNow.AddDays(-45) });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("skip AI"));

            var result = await _sut.GetRecommendationAsync(user.UserId);

            Assert.Equal("Khách hàng Gold hiện được miễn phí phủ bóng nhanh.", result);
        }

        [Fact]
        public async Task GetRecommendationAsync_AISucceeds_ReturnsAIRewrittenText()
        {
            var tier = new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 };
            _dbContext.Tiers.Add(tier);
            await _dbContext.SaveChangesAsync();

            var user = new User { PhoneNumber = "0980000005", Email = "rec5@test.com", PasswordHash = "x", Role = "Customer", Status = "Active" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.CustomerProfiles.Add(new CustomerProfile { UserId = user.UserId, FullName = "M", TierId = tier.TierId });
            await _dbContext.SaveChangesAsync();

            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("✨ Ưu đãi đặc biệt dành riêng cho bạn!");

            var result = await _sut.GetRecommendationAsync(user.UserId);

            Assert.Equal("✨ Ưu đãi đặc biệt dành riêng cho bạn!", result);
        }
    }
}