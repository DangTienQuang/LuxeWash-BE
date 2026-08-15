using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            return SendWithSmtpAsync(toEmail, subject, htmlMessage, null, null, null);
        }

        public Task SendEmailWithAttachmentAsync(
            string toEmail,
            string subject,
            string htmlMessage,
            byte[] attachment,
            string attachmentFileName,
            string attachmentContentType)
        {
            if (attachment == null || attachment.Length == 0)
                throw new ArgumentException("Email attachment cannot be empty.", nameof(attachment));

            return SendWithSmtpAsync(
                toEmail,
                subject,
                htmlMessage,
                attachment,
                attachmentFileName,
                attachmentContentType);
        }

        private async Task SendWithSmtpAsync(
            string toEmail,
            string subject,
            string htmlMessage,
            byte[]? attachment,
            string? attachmentFileName,
            string? attachmentContentType)
        {
            var smtpServer = _config["EmailSettings:SmtpServer"];
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var senderName = _config["EmailSettings:SenderName"] ?? "LuxeWash System";
            var username = _config["EmailSettings:Username"] ?? senderEmail;
            var password = _config["EmailSettings:Password"];
            var portValue = _config["EmailSettings:Port"];

            if (string.IsNullOrWhiteSpace(smtpServer))
                throw new InvalidOperationException("SMTP server is not configured.");
            if (!int.TryParse(portValue, out var port) || port <= 0)
                throw new InvalidOperationException("SMTP port is not configured correctly.");
            if (string.IsNullOrWhiteSpace(senderEmail))
                throw new InvalidOperationException("SMTP sender email is not configured.");
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("SMTP username is not configured.");
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("SMTP password is not configured.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            if (attachment != null)
            {
                builder.Attachments.Add(attachmentFileName ?? "invoice.pdf", attachment, ContentType.Parse(attachmentContentType ?? "application/pdf"));
            }

            message.Body = builder.ToMessageBody();

            using var smtpClient = new SmtpClient();
            smtpClient.Timeout = 20000;

            try
            {
                await smtpClient.ConnectAsync(smtpServer, port, SecureSocketOptions.Auto);
                await smtpClient.AuthenticateAsync(username, password);
                await smtpClient.SendAsync(message);
            }
            finally
            {
                await smtpClient.DisconnectAsync(true);
            }
        }
    }
}
