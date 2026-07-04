using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class InventoryTransaction
    {
        [Key]
        public int InventoryTransactionId { get; set; }

        public int WarehouseId { get; set; }

        [ForeignKey(nameof(WarehouseId))]
        public Warehouse Warehouse { get; set; } = null!;

        public int? BranchId { get; set; }

        [ForeignKey(nameof(BranchId))]
        public Branch? Branch { get; set; }

        public int MaterialId { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public Material Material { get; set; } = null!;

        public int? MaterialBatchId { get; set; }

        [ForeignKey(nameof(MaterialBatchId))]
        public MaterialBatch? MaterialBatch { get; set; }

        public int? BookingId { get; set; }

        [ForeignKey(nameof(BookingId))]
        public Booking? Booking { get; set; }

        [Required]
        [MaxLength(20)]
        public string TransactionType { get; set; } = null!;

        public decimal Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal CostAmount { get; set; }

        public decimal BeforeQuantity { get; set; }

        public decimal AfterQuantity { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public int? CreatedByUserId { get; set; }

        [ForeignKey(nameof(CreatedByUserId))]
        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
