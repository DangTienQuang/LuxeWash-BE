using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class PayOsService : IPayOsService
    {
        private readonly PayOSClient _payOSClient;

        public PayOsService(PayOSClient payOSClient)
        {
            _payOSClient = payOSClient;
        }

        public async Task<PayOsPaymentResult> CreatePaymentLinkAsync(
            long orderCode, int amount, string description, string userId,
            string? returnUrl = null, string? cancelUrl = null)
        {
            if (amount <= 0)
                throw new ArgumentException("Số tiền thanh toán PayOS phải lớn hơn 0.", nameof(amount));

            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = description,
                CancelUrl = string.IsNullOrWhiteSpace(cancelUrl) ? "http://localhost:5000/cancel" : cancelUrl,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "http://localhost:5000/success" : returnUrl
            };

            var response = await _payOSClient.PaymentRequests.CreateAsync(request);

            return new PayOsPaymentResult
            {
                CheckoutUrl = response.CheckoutUrl,
                OrderCode = orderCode
            };
        }

        public async Task<PayOsWebhookResult?> VerifyWebhookDataAsync(object webhookBody)
        {
            await Task.Yield();
            try
            {
                Webhook webhook = webhookBody switch
                {
                    Webhook w => w,
                    string s => JsonSerializer.Deserialize<Webhook>(s) ?? throw new InvalidOperationException("Cannot deserialize webhook body"),
                    _ => JsonSerializer.Deserialize<Webhook>(JsonSerializer.Serialize(webhookBody)) ?? throw new InvalidOperationException("Cannot deserialize webhook body")
                };

                var data = await _payOSClient.Webhooks.VerifyAsync(webhook);
                if (data == null)
                    return null;

                return new PayOsWebhookResult
                {
                    Code = data.Code,
                    OrderCode = data.OrderCode
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<PayOsOrderStatusResult?> GetPaymentStatusAsync(string orderCode)
        {
            if (string.IsNullOrWhiteSpace(orderCode))
                return null;

            if (!long.TryParse(orderCode, out var numericOrderCode))
                return null;

            try
            {
                dynamic paymentLink = await _payOSClient.PaymentRequests.GetAsync(numericOrderCode);

                string status = string.Empty;
                decimal amount = 0;
                DateTime? paidAt = null;

                try { status = paymentLink.Status?.ToString() ?? string.Empty; } catch { }
                try { amount = Convert.ToDecimal(paymentLink.Amount); } catch { }
                try { paidAt = paymentLink.PaidAt; } catch { }

                return new PayOsOrderStatusResult
                {
                    OrderCode = orderCode,
                    Status = status,
                    Amount = amount,
                    PaidAt = paidAt
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
