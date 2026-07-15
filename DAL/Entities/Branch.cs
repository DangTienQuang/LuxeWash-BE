using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class Branch
    {
        [Key]
        public int BranchId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;

        [MaxLength(255)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public bool AllowNegativeStock { get; set; }

        public decimal? NegativeStockLimit { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public ICollection<Lane> Lanes { get; set; } = new List<Lane>();
        public ICollection<EmployeeProfile> Employees { get; set; } = new List<EmployeeProfile>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
