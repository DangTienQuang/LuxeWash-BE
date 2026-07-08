using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class ServiceDTO
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public List<ServicePriceDTO> Prices { get; set; }
    }

    public class ServicePriceDTO
    {
        public int VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; }
        public int BranchId { get; set; }
        public decimal Price { get; set; }
        public int EstimatedDurationMinutes { get; set; }
    }
    public class CreateOrUpdateServiceDTO
    {
        [Required(ErrorMessage = "Service name is required.")]
        public string ServiceName { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Must configure at least 1 price for the service.")]
        [MinLength(1, ErrorMessage = "Must configure at least 1 price for the service.")]
        public List<CreateServicePriceDTO> Prices { get; set; }
    }

    public class CreateServicePriceDTO
    {
        [Required]
        public int VehicleTypeId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Price is invalid.")]
        public decimal Price { get; set; }

        [Required]
        [Range(5, 600, ErrorMessage = "Estimated duration (minutes) must be between 5 and 600.")]
        public int EstimatedDurationMinutes { get; set; }
    }
}