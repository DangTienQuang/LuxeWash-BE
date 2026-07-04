using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class FeatureDefinition
    {
        [Key]
        public int FeatureId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FeatureCode { get; set; } = string.Empty;
        // VISIT_COUNT

        [Required]
        [MaxLength(150)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string DataType { get; set; } = "Number";
        // Number
        // Decimal
        // Boolean
        // String
        // Date

        [MaxLength(100)]
        public string? SourceTable { get; set; }

        [MaxLength(500)]
        public string? CalculationMethod { get; set; }

        public bool IsAIFeature { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ScenarioCondition> ScenarioConditions { get; set; }
            = new List<ScenarioCondition>();

        public virtual ICollection<ScenarioExclusion> ScenarioExclusions { get; set; }
            = new List<ScenarioExclusion>();
    }
}