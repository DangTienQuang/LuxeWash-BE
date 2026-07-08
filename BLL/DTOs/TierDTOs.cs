using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class TierResponseDTO
    {
        public int TierId { get; set; }
        public string TierName { get; set; }
        public double PointMultiplier { get; set; }
        public int BookingWindowDays { get; set; }
        public int MinAccumulatedPoints { get; set; }
    }

    public class CreateTierDTO
    {
        [Required(ErrorMessage = "Tier name is required.")]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Tier name cannot consist of only whitespace.")]
        public string TierName { get; set; }

        [Range(1.0, 5.0, ErrorMessage = "Point multiplier must be between 1.0 and 5.0.")]
        public double PointMultiplier { get; set; }

        [Range(1, 30, ErrorMessage = "Booking window must be between 1 and 30 days.")]
        public int BookingWindowDays { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Minimum accumulated points is invalid.")]
        public int MinAccumulatedPoints { get; set; }
    }

    public class UpdateTierDTO : CreateTierDTO { }
}