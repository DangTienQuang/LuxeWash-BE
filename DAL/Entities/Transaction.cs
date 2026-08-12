using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DAL.Entities;

namespace AutoWashPro.DAL.Entities
{
    public class Transaction
    {
        [Key]
        public int TransactionId { get; set; }

        // WalletId is nullable for Walk-in guest payments
        public int? WalletId { get; set; }
        [ForeignKey("WalletId")]
        public Wallet? Wallet { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(20)]
        public required string TransactionType { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Description { get; set; }

        public int? ReferenceBookingId { get; set; }

        public int? ReferenceInvoiceId { get; set; }
        [ForeignKey("ReferenceInvoiceId")]
        public Invoice? ReferenceInvoice { get; set; }

        [MaxLength(100)]
        public string? OrderCode { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Completed"; // Completed, Pending

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
