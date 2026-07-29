using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ServicePrice
    {
        [Key]
        public int ServicePriceId { get; set; }

        [Required]
        public int ServiceId { get; set; }
        [ForeignKey("ServiceId")]
        public Service Service { get; set; } = null!;

        [Required]
        public int VehicleTypeId { get; set; }
        [ForeignKey("VehicleTypeId")]
        public VehicleType VehicleType { get; set; } = null!;

        [Required]
        public int BranchId { get; set; }
        [ForeignKey("BranchId")]
        public Branch Branch { get; set; } = null!;

        [Required]
        public decimal Price { get; set; }

        public int CapacityWeight { get; set; } = 1;

        public int EstimatedDurationMinutes { get; set; } = 30;
    }
}