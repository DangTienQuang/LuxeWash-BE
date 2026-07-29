using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class CarModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Brand { get; set; } = null!; // Hãng xe (VD: Toyota, Mazda)

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!; // Tên dòng xe (VD: Vios, CX-5)

        public bool IsActive { get; set; } = true;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Approved"; // Pending, Approved, Rejected

        public int? RequestedByUserId { get; set; }

        [ForeignKey("RequestedByUserId")]
        public User RequestedByUser { get; set; } = null!;

        public int? VehicleTypeId { get; set; }

        [ForeignKey("VehicleTypeId")]
        public VehicleType VehicleType { get; set; } = null!;
    }
}
