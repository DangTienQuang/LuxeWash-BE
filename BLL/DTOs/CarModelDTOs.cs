using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class CarModelDTO
    {
        public int Id { get; set; }
        public string Brand { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int? RequestedByUserId { get; set; }
        public int? VehicleTypeId { get; set; }
    }

    public class CreateCarModelDTO
    {
        [Required]
        public string Brand { get; set; } = null!;
        [Required]
        public string Name { get; set; } = null!;
        public int? VehicleTypeId { get; set; }
    }

    public class UpdateCarModelDTO
    {
        [Required]
        public string Brand { get; set; } = null!;
        [Required]
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    public class RequestCarModelDTO
    {
        [Required(ErrorMessage = "Please enter the car brand.")]
        public string Brand { get; set; } = null!;

        [Required(ErrorMessage = "Please enter the car model name.")]
        public string Name { get; set; } = null!;

        public int? Year { get; set; }
        public string? Version { get; set; }

        public int? VehicleTypeId { get; set; }
    }

    public class ApproveCarModelDTO
    {
        [Required(ErrorMessage = "Please select a vehicle type to approve.")]
        public int VehicleTypeId { get; set; }
    }
}
