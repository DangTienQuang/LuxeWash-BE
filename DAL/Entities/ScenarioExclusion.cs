using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ScenarioExclusion
    {
        [Key]
        public int ExclusionId { get; set; }

        public int ScenarioId { get; set; }

        public int FeatureId { get; set; }

        [ForeignKey(nameof(ScenarioId))]
        public virtual KnowledgeScenario Scenario { get; set; } = null!;

        [ForeignKey(nameof(FeatureId))]
        public virtual FeatureDefinition Feature { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Operator { get; set; } = "=";

        [Required]
        [MaxLength(200)]
        public string ComparisonValue { get; set; } = string.Empty;
    }
}