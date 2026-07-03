using System;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public interface IPayOsService
    {
        Task<PayOsPaymentResult> CreatePaymentLinkAsync(
            long orderCode,
            int amount,
            string description,
            string userId,
            string? returnUrl = null,
            string? cancelUrl = null);
        Task<PayOsWebhookResult?> VerifyWebhookDataAsync(object webhookBody);
        Task<PayOsOrderStatusResult?> GetPaymentStatusAsync(string orderCode);
    }

    public class PayOsPaymentResult
    {
        public string CheckoutUrl { get; set; } = null!;
        public long OrderCode { get; set; }
    }

    public class PayOsWebhookResult
    {
        public string Code { get; set; } = null!;
        public long OrderCode { get; set; }
        public bool IsSuccess => Code == "00";
    }

    public class PayOsOrderStatusResult
    {
        public string OrderCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime? PaidAt { get; set; }
        public bool IsPaid => string.Equals(Status, "PAID", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
        public bool IsCancelled => string.Equals(Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(Status, "EXPIRED", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(Status, "FAILED", StringComparison.OrdinalIgnoreCase);
    }
}
