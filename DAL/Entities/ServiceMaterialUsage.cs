using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ServiceMaterialUsage
    {
        [Key]
        public int ServiceMaterialUsageId { get; set; }

        public int ServiceId { get; set; }

        [ForeignKey(nameof(ServiceId))]
        public Service Service { get; set; } = null!;

        public int? VehicleTypeId { get; set; }

        [ForeignKey(nameof(VehicleTypeId))]
        public VehicleType? VehicleType { get; set; }

        public int MaterialId { get; set; }

        [ForeignKey(nameof(MaterialId))]
        public Material Material { get; set; } = null!;

        public decimal BaseQuantity { get; set; }

        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = null!;

        public bool IsActive { get; set; } = true;
    }
}
