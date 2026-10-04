using System;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class AdminTransactionQueryDTO
    {
        [Range(1, int.MaxValue / 100)] public int Page { get; set; } = 1;
        [Range(1, 100)] public int PageSize { get; set; } = 20;
        [MaxLength(100)] public string? Keyword { get; set; }
        [MaxLength(20)] public string? Status { get; set; }
        [MaxLength(20)] public string? TransactionType { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class AdminTransactionDTO
    {
        public int TransactionId { get; set; }
        public int? UserId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string? PaymentMethod { get; set; }
        public string? OrderCode { get; set; }
        public int? ReferenceBookingId { get; set; }
        public int? ReferenceInvoiceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
