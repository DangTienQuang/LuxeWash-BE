using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface IPayOsClient
    {
        Task<PaymentLinkResult> CreatePaymentLinkAsync(long orderCode, int amount, string description);
        Task<WebhookVerificationResult?> VerifyWebhookAsync(object webhookBody);
    }
    public class PaymentLinkResult
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public long OrderCode { get; set; }
    }
    public class WebhookVerificationResult
    {
        public string Code { get; set; } = string.Empty;
        public long OrderCode { get; set; }
    }
}
