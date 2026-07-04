using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class BookingMaterialUsage
    {
        [Key]
        public int BookingMaterialUsageId { get; set; }

        public int BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;

        public int? BookingDetailId { get; set; }

        [ForeignKey(nameof(BookingDetailId))]
        public BookingDetail? BookingDetail { get; set; }

        public int BranchId { get; set; }

        [ForeignKey(nameof(BranchId))]
        public Branch Branch { get; set; } = null!;

        public int MaterialId { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public Material Material { get; set; } = null!;

        public int? MaterialBatchId { get; set; }

        [ForeignKey(nameof(MaterialBatchId))]
        public MaterialBatch? MaterialBatch { get; set; }

        public decimal QuantityUsed { get; set; }

        public decimal UnitCost { get; set; }

        public decimal CostAmount { get; set; }

        public bool IsCostPending { get; set; }

        public decimal? EstimatedUnitCost { get; set; }

        [Required]
        [MaxLength(20)]
        public string UsageType { get; set; } = "Standard";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
