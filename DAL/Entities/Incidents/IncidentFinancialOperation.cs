using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class IncidentFinancialOperation
    {
        [Key]
        public long Id { get; set; }

        public long CaseId { get; set; }
        
        [ForeignKey("CaseId")]
        public virtual IncidentAffectedBooking Case { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Kind { get; set; } = null!; // "MoneyRefund", "PointsRefund", "RestoreOriginalVoucher", "CompensationVoucher"

        public decimal? Amount { get; set; }

        [MaxLength(100)]
        public string? ExternalReference { get; set; }

        [Required]
        public DateTime CreatedAtVn { get; set; } = DAL.Helpers.TimeHelper.VnNow;
    }
}
