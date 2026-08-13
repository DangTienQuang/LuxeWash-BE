using BLL.DTOs.Business;
using BLL.Helpers;
using BLL.Services.Interface;
using AutoWashPro.BLL.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class InvoiceDispatchService : IInvoiceDispatchService
    {
        private readonly IBusinessService _businessService;
        private readonly IInvoicePdfService _invoicePdfService;
        private readonly IEmailService _emailService;
        private readonly ILogger<InvoiceDispatchService> _logger;

        public InvoiceDispatchService(
            IBusinessService businessService,
            IInvoicePdfService invoicePdfService,
            IEmailService emailService,
            ILogger<InvoiceDispatchService> logger)
        {
            _businessService = businessService;
            _invoicePdfService = invoicePdfService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<InvoiceDispatchResponseDTO> SendInvoiceEmailAsync(int invoiceId)
        {
            var invoice = await _businessService.GetInvoiceExportAsync(invoiceId);
            if (string.IsNullOrWhiteSpace(invoice.BillingEmail))
                throw new InvalidOperationException("The business does not have a billing email.");

            var pdfBytes = await _invoicePdfService.GenerateInvoiceAsync(invoiceId);
            var fileName = InvoiceFileNameHelper.BuildInvoiceFileName(invoice);
            var company = WebUtility.HtmlEncode(invoice.BusinessName);
            var invoiceCode = WebUtility.HtmlEncode(invoice.InvoiceCode);
            var html = $@"
                <div style='font-family:Arial,sans-serif;line-height:1.6;color:#263238'>
                  <h2 style='color:#007493'>LuxeWash Pro - Business invoice</h2>
                  <p>Dear {company},</p>
                  <p>Your invoice <strong>{invoiceCode}</strong> has been issued.</p>
                  <p>Total amount: <strong>{invoice.TotalAmount:N0} VND</strong></p>
                  <p>The PDF invoice is attached. Sign in to the Business Portal, open <strong>Invoices</strong>, then choose Wallet or PayOS/QR to pay.</p>
                  <p>Thank you for using LuxeWash Pro.</p>
                </div>";

            try
            {
                await _emailService.SendEmailWithAttachmentAsync(
                    invoice.BillingEmail,
                    $"[LuxeWash Pro] Invoice {invoice.InvoiceCode}",
                    html,
                    pdfBytes,
                    fileName,
                    "application/pdf");

                return new InvoiceDispatchResponseDTO
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceCode = invoice.InvoiceCode,
                    Recipient = invoice.BillingEmail,
                    TotalAmount = invoice.TotalAmount,
                    EmailSent = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send invoice email for InvoiceId {InvoiceId} to {BillingEmail}", invoiceId, invoice.BillingEmail);
                throw;
            }
        }
    }
}
