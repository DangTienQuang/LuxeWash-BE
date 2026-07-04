using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class CustomerBehaviorHistory
    {
        [Key]
        public int BehaviorHistoryId { get; set; }
        [Required]
        public int CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual User Customer { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string BehaviorType { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? PreviousValue { get; set; }
        [MaxLength(200)]
        public string? CurrentValue { get; set; }
        public double Confidence { get; set; }
        [MaxLength(1000)]
        public string? Explanation { get; set; }

        [MaxLength(100)]
        public string DetectedBy { get; set; } = "AI Engine";
        public DateTime DetectedOn { get; set; } = DateTime.UtcNow;
    }
}