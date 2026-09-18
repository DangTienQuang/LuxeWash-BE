using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class LaneSlotCapacity
    {
        public int LaneId { get; set; }
        
        [ForeignKey("LaneId")]
        public virtual Lane Lane { get; set; } = null!;

        public int SlotId { get; set; }
        
        [ForeignKey("SlotId")]
        public virtual TimeSlot TimeSlot { get; set; } = null!;

        [Required]
        public int MaxWeightUnits { get; set; } = 0;

        public bool IsCalibrated { get; set; } = false;
    }
}
