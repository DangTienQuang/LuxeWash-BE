using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class WarehouseStock
    {
        [Key]
        public int WarehouseStockId { get; set; }

        public int WarehouseId { get; set; }

        [ForeignKey(nameof(WarehouseId))]
        public Warehouse Warehouse { get; set; } = null!;

        public int MaterialId { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public Material Material { get; set; } = null!;

        public decimal CurrentQuantity { get; set; }

        public decimal? MinStockLevel { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
