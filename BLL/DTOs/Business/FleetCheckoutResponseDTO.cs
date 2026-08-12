public class FleetCheckoutResponseDTO
{
    public int FleetWashLogId { get; set; }
    public int? InvoiceId { get; set; }
    public string? InvoiceCode { get; set; }
    public decimal TotalAmount { get; set; }
    public string BillingStatus { get; set; } = "PendingMonthlyInvoice";
    public DateTime CompletedTime { get; set; }
}
