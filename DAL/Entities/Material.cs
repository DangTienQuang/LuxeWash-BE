using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class Material
    {
        [Key]
        public int MaterialId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Unit { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool RequiresExpiryTracking { get; set; }

        public decimal DefaultMinStockLevel { get; set; }

        public int ExpiryWarningDays { get; set; } = 30;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<MaterialBatch> Batches { get; set; } = new List<MaterialBatch>();
    }
}
