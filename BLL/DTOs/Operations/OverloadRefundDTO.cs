namespace AutoWashPro.BLL.DTOs
{
    public class OverloadRefundDTO
    {
        public decimal RefundedAmount { get; set; }
        public string? RefundDestination { get; set; }
        public int RefundedPoints { get; set; }
        public int? RestoredVoucherId { get; set; }
    }
}
