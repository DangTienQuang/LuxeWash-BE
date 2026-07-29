using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class VehicleType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int BaseWeight { get; set; } = 1;

        public ICollection<Vehicle> Vehicles { get; set; } = null!;
    }
}