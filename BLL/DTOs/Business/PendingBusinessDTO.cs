using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Business
{
    public class PendingBusinessApplicationDTO
    {
        public int BusinessProfileId { get; set; }
        public string CompanyName { get; set; } = null!;
        public string? TaxCode { get; set; }
        public string? BusinessAddress { get; set; }
        public string? BillingEmail { get; set; }
        public string? RepresentativeName { get; set; }
        public string ApprovalStatus { get; set; } = null!;
        public string? RejectionReason { get; set; }
        public string BusinessLicenseFileUrl { get; set; } = null!;
        public string? AuthorizationLetterFileUrl { get; set; }
        public int? PaymentTermDays { get; set; }
        public decimal MonthlyCreditLimit { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
