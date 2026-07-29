using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class Tier
    {
        [Key]
        public int TierId { get; set; }

        [Required]
        [MaxLength(50)]
        public string TierName { get; set; } = null!;

        public double PointMultiplier { get; set; }

        public int BookingWindowDays { get; set; }

        [Required]
        public int MinAccumulatedPoints { get; set; }

        public ICollection<CustomerProfile> CustomerProfiles { get; set; } = null!;
    }
}