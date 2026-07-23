using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using BLL.Constants;
using BLL.Services;
using AutoWashPro.BLL.Services;

namespace AutoWashPro.Tests.BLL
{
    public class AIIntentServiceTests
    {
        private readonly Mock<ILLMService> _llmMock;
        private readonly AIIntentService _sut;

        public AIIntentServiceTests()
        {
            _llmMock = new Mock<ILLMService>();
            _sut = new AIIntentService(_llmMock.Object);
        }

        [Theory]
        [InlineData("CHECK_POINTS")]
        [InlineData("CHECK_TIER")]
        [InlineData("LAST_VISIT")]
        [InlineData("REFERRAL")]
        [InlineData("RECOMMENDATION")]
        [InlineData("UNKNOWN")]
        public async Task DetectIntentAsync_ExactValidIntent_ReturnsSameIntent(string validIntent)
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(validIntent);

            var result = await _sut.DetectIntentAsync("some message");

            Assert.Equal(validIntent, result);
        }

        [Fact]
        public async Task DetectIntentAsync_LowercaseValidIntent_ShouldNormalizeToUppercase()
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("check_tier");

            var result = await _sut.DetectIntentAsync("hạng của tôi");

            Assert.Equal(AIIntent.CheckTier, result);
        }

        [Fact]
        public async Task DetectIntentAsync_ValidIntentWithWhitespace_ShouldTrim()
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("  REFERRAL  \n");

            var result = await _sut.DetectIntentAsync("mã giới thiệu");

            Assert.Equal(AIIntent.Referral, result);
        }

        [Fact]
        public async Task DetectIntentAsync_UnrecognizedText_ReturnsUnknown()
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("I have no idea what this means");

            var result = await _sut.DetectIntentAsync("gibberish input");

            Assert.Equal(AIIntent.Unknown, result);
        }

        [Fact]
        public async Task DetectIntentAsync_LLMThrows_ReturnsUnknown()
        {
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("LLM unavailable"));

            var result = await _sut.DetectIntentAsync("any message");

            Assert.Equal(AIIntent.Unknown, result);
        }

        [Fact]
        public async Task DetectIntentAsync_PassesOriginalMessageIntoPrompt()
        {
            string capturedUserPrompt = null;
            _llmMock.Setup(l => l.GenerateReplyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((system, user) => capturedUserPrompt = user)
                .ReturnsAsync(AIIntent.Unknown);

            await _sut.DetectIntentAsync("test message content");

            Assert.Contains("test message content", capturedUserPrompt);
        }
    }
}