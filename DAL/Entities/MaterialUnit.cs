using System;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class MaterialUnit
    {
        [Key]
        public int UnitId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string DisplayName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string MeasurementType { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
