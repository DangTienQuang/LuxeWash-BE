using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class EmailService : IEmailService
    {
        private static readonly HttpClient SendGridHttpClient = new()
        {
            BaseAddress = new Uri("https://api.sendgrid.com"),
            Timeout = TimeSpan.FromSeconds(20)
        };

        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var sendGridApiKey = _config["SendGridSettings:ApiKey"];
            if (string.IsNullOrWhiteSpace(sendGridApiKey))
                throw new InvalidOperationException("SendGrid API key is not configured.");

            await SendWithSendGridAsync(sendGridApiKey, toEmail, subject, htmlMessage);
        }

        private async Task SendWithSendGridAsync(string apiKey, string toEmail, string subject, string htmlMessage)
        {
            var senderName = _config["SendGridSettings:SenderName"] ?? _config["EmailSettings:SenderName"] ?? "SmartWash System";
            var senderEmail = _config["SendGridSettings:SenderEmail"] ?? _config["EmailSettings:SenderEmail"];

            if (string.IsNullOrWhiteSpace(senderEmail))
                throw new InvalidOperationException("SendGrid sender email is not configured.");

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[]
                        {
                            new
                            {
                                email = toEmail
                            }
                        }
                    }
                },
                from = new
                {
                    email = senderEmail,
                    name = senderName
                },
                subject,
                content = new[]
                {
                    new
                    {
                        type = "text/html",
                        value = htmlMessage
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v3/mail/send");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("accept", "application/json");
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await SendGridHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"SendGrid email failed with status {(int)response.StatusCode}: {error}");
            }
        }

    }
}