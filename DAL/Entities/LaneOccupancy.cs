using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class LaneOccupancy
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LaneId { get; set; }

        [Required]
        public int BranchId { get; set; }

        public int? BookingId { get; set; }
        
        public int? FleetWashLogId { get; set; }

        [Required]
        [MaxLength(20)]
        public string LicensePlate { get; set; } = null!;

        [Required]
        public DateTime OccupiedAt { get; set; }

        [ForeignKey("LaneId")]
        public virtual Lane Lane { get; set; } = null!;

        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; }
        
        [ForeignKey("FleetWashLogId")]
        public virtual global::DAL.Entities.FleetWashLog? FleetWashLog { get; set; }
    }
}
