using BLL.DTOs.Business;
using BLL.Helpers;
using BLL.Services;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public InvoiceController(IBusinessBookingService businessBookingService, IBusinessService businessService,
            IInvoicePdfService invoicePdfService)
        {
            _businessBookingService = businessBookingService;
            _businessService = businessService;
            _invoicePdfService = invoicePdfService;
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
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> GenerateMonthlyInvoice(GenerateMonthlyInvoiceRequest request)
        {
            var invoiceId = await _businessService.GenerateMonthlyInvoiceAsync(
                    request.BusinessProfileId,
                    request.Year,
                    request.Month);

            return Ok(new
            {
                statusCode = 200,
                message = "Monthly invoice generated successfully.",
                InvoiceId = invoiceId
            });
        }
    }
}
