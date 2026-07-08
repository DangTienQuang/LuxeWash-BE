using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class VehicleTypeDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public int BaseWeight { get; set; }
    }

    public class CreateVehicleTypeDTO
    {
        [Required(ErrorMessage = "Vehicle type name is required.")]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Vehicle type name cannot consist of only whitespace.")]
        public string Name { get; set; }

        public string? Description { get; set; }

        [Range(0, 100)]
        public int BaseWeight { get; set; } = 1;
    }
}