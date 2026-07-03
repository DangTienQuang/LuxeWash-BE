using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class VehicleConditionMaterialMultiplier
    {
        [Key]
        public int Id { get; set; }

        public VehicleCondition VehicleCondition { get; set; }

        public decimal Multiplier { get; set; } = 1;

        [MaxLength(255)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
