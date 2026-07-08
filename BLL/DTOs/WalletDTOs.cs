using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoWashPro.BLL.DTOs
{
    public class WalletResponseDTO
    {
        public decimal Balance { get; set; }
        public int TotalPoints { get; set; }
        public int PromotionPoints { get; set; }
    }

    public class TopUpRequestDTO
    {
        [Required]
        [Range(typeof(decimal), "1", "1000000000", ErrorMessage = "Top-up amount is invalid.")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string CancelUrl { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string ReturnUrl { get; set; }
    }

    public class TopUpResponseDTO
    {
        public required string PaymentUrl { get; set; }
        public required string OrderCode { get; set; }
    }

    public class PaymentQrRequestDTO
    {
        [Required]
        [RegularExpression("^(Topup|TopUp|BookingPayment|Booking)$", ErrorMessage = "PaymentType only supports Topup or BookingPayment.")]
        public required string PaymentType { get; set; }

        [Range(typeof(decimal), "1", "1000000000", ErrorMessage = "Payment amount is invalid.")]
        public decimal? Amount { get; set; }

        public int? BookingId { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string CancelUrl { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string ReturnUrl { get; set; }
    }

    public class PaymentQrResponseDTO
    {
        public required string PaymentUrl { get; set; }
        public required string OrderCode { get; set; }
        public required string PaymentType { get; set; }
        public decimal Amount { get; set; }
        public int? BookingId { get; set; }
    }

    public class TransactionResponseDTO
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public required string TransactionType { get; set; }
        public required string Description { get; set; }
        public string Status { get; set; } = "Completed";
        public string? OrderCode { get; set; }
        public int? ReferenceBookingId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PointHistoryResponseDTO
    {
        public int LedgerId { get; set; }
        public int PointsAdded { get; set; }
        public int PointsDeducted { get; set; }
        public required string Reason { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    public class WebhookTopUpDTO
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "00";

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        [JsonPropertyName("data")]
        public WebhookDataDTO? Data { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = "test-signature-ignore-on-local";
    }

    public class WebhookDataDTO
    {
        [JsonPropertyName("orderCode")]
        public long OrderCode { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("accountNumber")]
        public string? AccountNumber { get; set; }

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }

        [JsonPropertyName("transactionDateTime")]
        public string? TransactionDateTime { get; set; }

        [JsonPropertyName("virtualAccountNumber")]
        public string? VirtualAccountNumber { get; set; }

        [JsonPropertyName("counterAccountBankId")]
        public string? CounterAccountBankId { get; set; }

        [JsonPropertyName("counterAccountBankName")]
        public string? CounterAccountBankName { get; set; }

        [JsonPropertyName("counterAccountName")]
        public string? CounterAccountName { get; set; }

        [JsonPropertyName("counterAccountNumber")]
        public string? CounterAccountNumber { get; set; }

        [JsonPropertyName("virtualAccountName")]
        public string? VirtualAccountName { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("paymentLinkId")]
        public string? PaymentLinkId { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("desc")]
        public string? Desc { get; set; }
    }
}
