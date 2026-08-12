using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
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
            var senderName = _config["EmailSettings:SenderName"] ?? "SmartWash System";
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

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            if (attachment != null)
            {
                var stream = new MemoryStream(attachment, writable: false);
                message.Attachments.Add(new Attachment(
                    stream,
                    attachmentFileName ?? "invoice.pdf",
                    attachmentContentType ?? "application/pdf"));
            }

            using var smtpClient = new SmtpClient(smtpServer, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
            };

            try
            {
                await smtpClient.SendMailAsync(message);
            }
            catch (SmtpException ex)
            {
                throw new InvalidOperationException(
                    $"SMTP email delivery failed ({ex.StatusCode}).",
                    ex);
            }
        }
    }
}
