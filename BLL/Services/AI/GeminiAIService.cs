using AutoWashPro.BLL.Services;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace BLL.Services
{
    public class GeminiAIService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GeminiAIService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GenerateReplyAsync(
            string systemPrompt,
            string userPrompt)
        {
            var apiKey =
                _configuration["Gemini:ApiKey"];

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text =
                                    $"{systemPrompt}\n\n{userPrompt}"
                            }
                        }
                    }
                }
            };

            var json =
                JsonSerializer.Serialize(body);

            var response =
                await _httpClient.PostAsync(
                    url,
                    new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json"));

            response.EnsureSuccessStatusCode();

            var responseJson =
                await response.Content
                    .ReadAsStringAsync();

            using var doc =
                JsonDocument.Parse(responseJson);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()
                ?? "AI không phản hồi.";
        }
    }
}