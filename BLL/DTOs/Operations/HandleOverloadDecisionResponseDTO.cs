namespace AutoWashPro.BLL.DTOs
{
    public class HandleOverloadDecisionResponseDTO
    {
        public bool Success { get; set; }
        public string Decision { get; set; } = null!;
        public string Message { get; set; } = null!;
        public BookingResponseDTO? UpdatedBooking { get; set; }
        public VoucherResponseDTO? Voucher { get; set; }
        public OverloadRefundDTO? Refund { get; set; }
    }
}
