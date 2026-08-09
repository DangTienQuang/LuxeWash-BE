public class FleetCheckoutResponseDTO
{
    public int FleetWashLogId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public DateTime CompletedTime { get; set; }
}