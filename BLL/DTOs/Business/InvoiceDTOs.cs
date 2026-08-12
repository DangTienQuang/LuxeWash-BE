using BLL.DTOs.Fleet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Business
{
    public class InvoiceDTO
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = null!;
        public string InvoiceType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime IssuedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<InvoiceItemDTO> Items { get; set; } = new();
    }
    public class InvoiceItemDTO
    {
        public int InvoiceItemId { get; set; }
        public string Description { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }
    public class InvoiceListDTO
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = null!;
        public DateTime IssuedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public string? LicensePlate { get; set; }
        public string InvoiceType { get; set; } = null!;
    }
    public class InvoiceDetailDTO
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = null!;
        public DateTime IssuedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public string? LicensePlate { get; set; }
        public string InvoiceType { get; set; } = null!;
        public List<InvoiceItemDTO> Items { get; set; } = new();
    }
    public class MonthlyStatementDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalWashes { get; set; }
        public decimal TotalCost { get; set; }
        public List<VehicleStatementDTO> Vehicles { get; set; } = new();
    }
    public class GenerateMonthlyInvoiceRequest
    {
        public int BusinessProfileId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
    }
    public class BillingBusinessDTO
    {
        public int BusinessProfileId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string BillingEmail { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
    }
    public class InvoiceDispatchResponseDTO
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public bool EmailSent { get; set; }
        public string? EmailError { get; set; }
    }
    public class InvoicePaymentRequestDTO
    {
        public string PaymentMethod { get; set; } = "PayOS";
        public string? ReturnUrl { get; set; }
        public string? CancelUrl { get; set; }
    }
    public class InvoicePaymentResponseDTO
    {
        public int InvoiceId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentUrl { get; set; }
        public string? OrderCode { get; set; }
        public decimal Amount { get; set; }
    }
    public class InvoiceExportDTO
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string InvoiceType { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string BusinessAddress { get; set; } = string.Empty;
        public string BillingEmail { get; set; } = string.Empty;
        public string RepresentativeName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string BranchAddress { get; set; } = string.Empty;
        public int BookingId { get; set; }
        public string BillingPeriod { get; set; } = string.Empty;
        public List<InvoiceItemDTO> Items { get; set; } = new();
    }
}
