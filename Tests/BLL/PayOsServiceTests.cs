using System;
using System.Threading.Tasks;
using Xunit;
using AutoWashPro.BLL.Services;

namespace AutoWashPro.Tests.BLL
{
    public class PayOsServiceTests
    {
        private readonly PayOsService _sut;

        public PayOsServiceTests()
        {
            _sut = new PayOsService(null!); // guard clauses fire before the client is touched
        }

        [Fact]
        public async Task CreatePaymentLinkAsync_ZeroAmount_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreatePaymentLinkAsync(123, 0, "desc", "user1"));
        }

        [Fact]
        public async Task CreatePaymentLinkAsync_NegativeAmount_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreatePaymentLinkAsync(123, -100, "desc", "user1"));
        }

        [Fact]
        public async Task GetPaymentStatusAsync_EmptyOrderCode_ReturnsNull()
        {
            var result = await _sut.GetPaymentStatusAsync("");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPaymentStatusAsync_NonNumericOrderCode_ReturnsNull()
        {
            var result = await _sut.GetPaymentStatusAsync("not-a-number");

            Assert.Null(result);
        }

        [Fact]
        public async Task VerifyWebhookDataAsync_InvalidBody_ReturnsNull()
        {
            var result = await _sut.VerifyWebhookDataAsync(new object());

            Assert.Null(result);
        }
    }
}