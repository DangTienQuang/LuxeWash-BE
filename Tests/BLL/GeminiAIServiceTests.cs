using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using BLL.Services;

namespace AutoWashPro.Tests.BLL
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    public class GeminiAIServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;

        public GeminiAIServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["Gemini:ApiKey"]).Returns("test-api-key");
        }

        private GeminiAIService CreateService(HttpStatusCode statusCode, string responseBody, out FakeHttpMessageHandler handler)
        {
            handler = new FakeHttpMessageHandler(statusCode, responseBody);
            var httpClient = new HttpClient(handler);
            return new GeminiAIService(httpClient, _configMock.Object);
        }

        [Fact]
        public async Task GenerateReplyAsync_ValidResponse_ReturnsExtractedText()
        {
            var responseJson = """
            {
                "candidates": [
                    { "content": { "parts": [ { "text": "Xin chào, tôi có thể giúp gì cho bạn?" } ] } }
                ]
            }
            """;
            var service = CreateService(HttpStatusCode.OK, responseJson, out _);

            var result = await service.GenerateReplyAsync("system prompt", "user prompt");

            Assert.Equal("Xin chào, tôi có thể giúp gì cho bạn?", result);
        }

        [Fact]
        public async Task GenerateReplyAsync_NullTextInResponse_ReturnsFallbackMessage()
        {
            var responseJson = """
            {
                "candidates": [
                    { "content": { "parts": [ { "text": null } ] } }
                ]
            }
            """;
            var service = CreateService(HttpStatusCode.OK, responseJson, out _);

            var result = await service.GenerateReplyAsync("system prompt", "user prompt");

            Assert.Equal("AI không phản hồi.", result);
        }

        [Fact]
        public async Task GenerateReplyAsync_NonSuccessStatusCode_ThrowsHttpRequestException()
        {
            var service = CreateService(HttpStatusCode.InternalServerError, "{}", out _);

            await Assert.ThrowsAsync<HttpRequestException>(() => service.GenerateReplyAsync("system", "user"));
        }

        [Fact]
        public async Task GenerateReplyAsync_RateLimited_ThrowsHttpRequestException()
        {
            var service = CreateService(HttpStatusCode.TooManyRequests, "{}", out _);

            await Assert.ThrowsAsync<HttpRequestException>(() => service.GenerateReplyAsync("system", "user"));
        }

        [Fact]
        public async Task GenerateReplyAsync_MalformedJson_ThrowsJsonException()
        {
            var service = CreateService(HttpStatusCode.OK, "this is not json {{{", out _);

            // JsonDocument.Parse throws the more specific JsonReaderException,
            // which derives from JsonException — assert the base type for robustness
            var ex = await Assert.ThrowsAnyAsync<JsonException>(() => service.GenerateReplyAsync("system", "user"));
            Assert.IsAssignableFrom<JsonException>(ex);
        }

        [Fact]
        public async Task GenerateReplyAsync_MissingCandidatesProperty_ThrowsKeyNotFoundException()
        {
            var service = CreateService(HttpStatusCode.OK, "{}", out _);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GenerateReplyAsync("system", "user"));
        }

        [Fact]
        public async Task GenerateReplyAsync_RequestUrl_IncludesApiKeyFromConfig()
        {
            var responseJson = """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""";
            var service = CreateService(HttpStatusCode.OK, responseJson, out var handler);

            await service.GenerateReplyAsync("system", "user");

            Assert.NotNull(handler.LastRequest);
            Assert.Contains("key=test-api-key", handler.LastRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task GenerateReplyAsync_RequestBody_ContainsBothPrompts()
        {
            var responseJson = """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""";
            var service = CreateService(HttpStatusCode.OK, responseJson, out var handler);

            await service.GenerateReplyAsync("You are a helpful assistant", "What is the weather?");

            var sentBody = await handler.LastRequest!.Content!.ReadAsStringAsync();
            Assert.Contains("You are a helpful assistant", sentBody);
            Assert.Contains("What is the weather?", sentBody);
        }
    }
}