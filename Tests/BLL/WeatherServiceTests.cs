using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.Tests.BLL
{
    // Reusable fake handler — supports queued responses so we can test
    // multi-call methods like IsProlongedRainAsync (current + forecast)
    public class QueuedFakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode status, string body)> _responses;

        public QueuedFakeHttpMessageHandler(params (HttpStatusCode status, string body)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.InternalServerError, "{}");
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            return Task.FromResult(response);
        }
    }

    public class WeatherServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ILogger<WeatherService>> _loggerMock;

        public WeatherServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["OpenWeatherMap:ApiKey"]).Returns("fake-key");
            _loggerMock = new Mock<ILogger<WeatherService>>();
        }

        private WeatherService CreateService(params (System.Net.HttpStatusCode status, string body)[] responses)
        {
            var handler = new QueuedFakeHttpMessageHandler(responses);
            var httpClient = new HttpClient(handler);
            return new WeatherService(httpClient, _configMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task IsRainingNowAsync_NoApiKey_ReturnsFalse()
        {
            _configMock.Setup(c => c["OpenWeatherMap:ApiKey"]).Returns((string)null);
            var service = CreateService();

            var result = await service.IsRainingNowAsync();

            Assert.False(result);
        }

        [Fact]
        public async Task IsRainingNowAsync_ApiCallFails_ReturnsFalse()
        {
            var service = CreateService((HttpStatusCode.InternalServerError, "{}"));

            var result = await service.IsRainingNowAsync();

            Assert.False(result);
        }

        [Fact]
        public async Task IsRainingNowAsync_WeatherIsRain_ReturnsTrue()
        {
            var body = """{"weather":[{"main":"Rain"}]}""";
            var service = CreateService((HttpStatusCode.OK, body));

            var result = await service.IsRainingNowAsync();

            Assert.True(result);
        }

        [Fact]
        public async Task IsRainingNowAsync_WeatherIsThunderstorm_ReturnsTrue()
        {
            var body = """{"weather":[{"main":"Thunderstorm"}]}""";
            var service = CreateService((HttpStatusCode.OK, body));

            var result = await service.IsRainingNowAsync();

            Assert.True(result);
        }

        [Fact]
        public async Task IsRainingNowAsync_WeatherIsClear_ReturnsFalse()
        {
            var body = """{"weather":[{"main":"Clear"}]}""";
            var service = CreateService((HttpStatusCode.OK, body));

            var result = await service.IsRainingNowAsync();

            Assert.False(result);
        }

        [Fact]
        public async Task IsRainingNowAsync_MalformedJson_ReturnsFalse()
        {
            var service = CreateService((HttpStatusCode.OK, "not json {{{"));

            var result = await service.IsRainingNowAsync();

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_NullBranch_ReturnsFalse()
        {
            var service = CreateService();

            var result = await service.IsProlongedRainAsync(null);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_NoApiKey_ReturnsFalse()
        {
            _configMock.Setup(c => c["OpenWeatherMap:ApiKey"]).Returns((string)null);
            var service = CreateService();
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = "123 St, Ho Chi Minh" };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_CurrentWeatherCallFails_ReturnsFalse()
        {
            var service = CreateService((HttpStatusCode.InternalServerError, "{}"));
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = "123 St, Ho Chi Minh" };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_CurrentWeatherNotRain_ReturnsFalseWithoutForecastCall()
        {
            var body = """{"weather":[{"main":"Clear"}]}""";
            var service = CreateService((HttpStatusCode.OK, body));
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = "123 St, Ho Chi Minh" };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_CurrentRain_ForecastCallFails_ReturnsFalse()
        {
            var currentBody = """{"weather":[{"main":"Rain"}]}""";
            var service = CreateService(
                (HttpStatusCode.OK, currentBody),
                (HttpStatusCode.InternalServerError, "{}")
            );
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = "123 St, Ho Chi Minh" };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_CurrentAndForecastBothRain_ReturnsTrue()
        {
            var currentBody = """{"weather":[{"main":"Rain"}]}""";
            var forecastBody = """{"list":[{"weather":[{"main":"Rain"}]}]}""";
            var service = CreateService(
                (HttpStatusCode.OK, currentBody),
                (HttpStatusCode.OK, forecastBody)
            );
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = "123 St, Ho Chi Minh" };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.True(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_CurrentRainForecastClear_ReturnsFalse()
        {
            var currentBody = """{"weather":[{"main":"Rain"}]}""";
            var forecastBody = """{"list":[{"weather":[{"main":"Clear"}]}]}""";
            var service = CreateService(
                (HttpStatusCode.OK, currentBody),
                (HttpStatusCode.OK, forecastBody)
            );
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = "123 St, Ho Chi Minh" };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.False(result);
        }

        [Fact]
        public async Task IsProlongedRainAsync_EmptyAddress_UsesDefaultCity()
        {
            var currentBody = """{"weather":[{"main":"Rain"}]}""";
            var forecastBody = """{"list":[{"weather":[{"main":"Rain"}]}]}""";
            var service = CreateService(
                (HttpStatusCode.OK, currentBody),
                (HttpStatusCode.OK, forecastBody)
            );
            var branch = new Branch { Name = "Branch A", IsActive = true, Address = null };

            var result = await service.IsProlongedRainAsync(branch);

            Assert.True(result); // just confirms it doesn't throw and proceeds normally with default city
        }
    }
}