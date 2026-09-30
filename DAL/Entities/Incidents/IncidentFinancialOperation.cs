using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public static class IncidentFinancialOperationKinds
    {
        public const string MoneyRefund = "MoneyRefund";
        public const string PointsRefund = "PointsRefund";
        public const string RestoreOriginalVoucher = "RestoreOriginalVoucher";
        public const string CompensationVoucher = "CompensationVoucher";
        public const string BusinessCreditRelease = "BusinessCreditRelease";
    }

    public class IncidentFinancialOperation
    {
        [Key]
        public long Id { get; set; }

        public long CaseId { get; set; }
        
        [ForeignKey("CaseId")]
        public virtual IncidentAffectedBooking Case { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Kind { get; set; } = null!;

        public decimal? Amount { get; set; }

        [MaxLength(100)]
        public string? ExternalReference { get; set; }

        [Required]
        public DateTime CreatedAtVn { get; set; } = DAL.Helpers.TimeHelper.VnNow;
    }
}
