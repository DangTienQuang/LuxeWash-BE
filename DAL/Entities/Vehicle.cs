using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class Vehicle
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; } // THÊM C?T NÀY LÀM KHÓA CHÍNH (Ki?u int)

        [Required]
        [MaxLength(20)]
        public string LicensePlate { get; set; } = null!; // ÐÃ B? [Key] ? ÐÂY

        [ForeignKey("User")]
        public int? UserId { get; set; }
        public User User { get; set; } = null!;

        [Required]
        public int VehicleTypeId { get; set; }

        [ForeignKey("VehicleTypeId")]
        public VehicleType VehicleType { get; set; } = null!;

        public string? RegistrationPhotoUrl { get; set; }

        public string? UserNote { get; set; }

        public int? CarModelId { get; set; }

        [ForeignKey("CarModelId")]
        public CarModel CarModelEntity { get; set; } = null!;

        public string? CarModel { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}