using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using AutoWashPro.BLL.Services;

namespace AutoWashPro.Tests.BLL
{
    public class EmailServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly EmailService _sut;

        public EmailServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _sut = new EmailService(_configMock.Object);
        }

        [Fact]
        public async Task SendEmailAsync_NoApiKeyConfigured_ThrowsInvalidOperationException()
        {
            _configMock.Setup(c => c["SendGridSettings:ApiKey"]).Returns((string)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SendEmailAsync("test@test.com", "Subject", "<p>Body</p>"));
        }

        [Fact]
        public async Task SendEmailAsync_NoSenderEmailConfigured_ThrowsInvalidOperationException()
        {
            _configMock.Setup(c => c["SendGridSettings:ApiKey"]).Returns("fake-api-key");
            _configMock.Setup(c => c["SendGridSettings:SenderEmail"]).Returns((string)null);
            _configMock.Setup(c => c["EmailSettings:SenderEmail"]).Returns((string)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SendEmailAsync("test@test.com", "Subject", "<p>Body</p>"));
        }
    }
}