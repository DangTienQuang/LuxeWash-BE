using BLL.DTOs.Business;
using BLL.Helpers;
using BLL.Services;
using BLL.Services.Interface;
using AutoWashPro.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace API.Controllers.User
{
    [ApiController]
    [Route("api/v1/invoice")]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly IBusinessBookingService _businessBookingService;
        private readonly IBusinessService _businessService;
        private readonly IInvoicePdfService _invoicePdfService;
        private readonly IEmailService _emailService;
        private readonly IWalletService _walletService;

        public InvoiceController(IBusinessBookingService businessBookingService, IBusinessService businessService,
            IInvoicePdfService invoicePdfService, IEmailService emailService, IWalletService walletService)
        {
            _businessBookingService = businessBookingService;
            _businessService = businessService;
            _invoicePdfService = invoicePdfService;
            _emailService = emailService;
            _walletService = walletService;
        }

        [Authorize(Roles = "Business, Manager")]
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices()
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetInvoicesAsync(userId);

            return Ok(result);
        }

        [Authorize(Roles = "Business, Manager")]
        [HttpGet("invoices/{invoiceId}")]
        public async Task<IActionResult> GetInvoiceDetail(int invoiceId)
        {
            int userId = ClaimHelper.GetUserId(User);

            var result = await _businessBookingService.GetInvoiceDetailAsync(userId, invoiceId);

            return Ok(result);
        }

        [HttpGet("invoices/{invoiceId}/pdf")]
        [Authorize(Roles = "Business,Manager,Staff")]
        public async Task<IActionResult> DownloadInvoicePdf(int invoiceId)
        {
            var invoice = await _businessService.GetInvoiceExportAsync(invoiceId);
            var pdfBytes = await _invoicePdfService.GenerateInvoiceAsync(invoiceId);
            var fileName = InvoiceFileNameHelper.BuildInvoiceFileName(invoice);

            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpPost("billing/monthly")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateMonthlyInvoice(GenerateMonthlyInvoiceRequest request)
        {
            var invoiceId = await _businessService.GenerateMonthlyInvoiceAsync(
                    request.BusinessProfileId,
                    request.Year,
                    request.Month);

            InvoiceDispatchResponseDTO dispatch;
            try
            {
                dispatch = await SendInvoiceEmailAsync(invoiceId);
            }
            catch (Exception ex)
            {
                var invoice = await _businessService.GetInvoiceExportAsync(invoiceId);
                dispatch = new InvoiceDispatchResponseDTO
                {
                    InvoiceId = invoice.InvoiceId,
                    InvoiceCode = invoice.InvoiceCode,
                    Recipient = invoice.BillingEmail,
                    TotalAmount = invoice.TotalAmount,
                    EmailSent = false,
                    EmailError = ex.Message
                };
            }

            return Ok(new
            {
                statusCode = 200,
                message = dispatch.EmailSent
                    ? "Monthly invoice generated and sent successfully."
                    : "Monthly invoice generated, but the email could not be sent. It can be resent using the same invoice.",
                data = dispatch
            });
        }

        [HttpGet("billing/businesses")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetBillingBusinesses()
        {
            var result = await _businessService.GetBillingBusinessesAsync();
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpPost("invoices/{invoiceId}/send")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResendInvoice(int invoiceId)
        {
            var dispatch = await SendInvoiceEmailAsync(invoiceId);
            return Ok(new
            {
                statusCode = 200,
                message = "Invoice sent successfully.",
                data = dispatch
            });
        }

        [HttpPost("invoices/{invoiceId}/pay")]
        [Authorize(Roles = "Business")]
        public async Task<IActionResult> PayInvoice(
            int invoiceId,
            [FromBody] InvoicePaymentRequestDTO request)
        {
            var userId = ClaimHelper.GetUserId(User);
            var result = await _walletService.CreateInvoicePaymentAsync(userId, invoiceId, request);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        [HttpGet("invoices/{invoiceId}/payment-status")]
        [Authorize(Roles = "Business")]
        public async Task<IActionResult> GetInvoicePaymentStatus(int invoiceId)
        {
            var userId = ClaimHelper.GetUserId(User);
            var result = await _walletService.GetInvoicePaymentStatusAsync(userId, invoiceId);
            return Ok(new { statusCode = 200, message = "Success", data = result });
        }

        private async Task<InvoiceDispatchResponseDTO> SendInvoiceEmailAsync(int invoiceId)
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
    }
}
