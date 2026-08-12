using AutoWashPro.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Invoice
    {
        public int InvoiceId { get; set; }

        public string InvoiceCode { get; set; } = null!;

        public int? BookingId { get; set; }

        public int? BusinessProfileId { get; set; }

        public string InvoiceType { get; set; } = "Service";

        public decimal Subtotal { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime IssuedAt { get; set; }

        public Booking? Booking { get; set; } = null!;

        public BusinessProfile? BusinessProfile { get; set; }

        public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
