using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class KnowledgeScenario
    {
        [Key]
        public int ScenarioId { get; set; }

        public int CategoryId { get; set; }

        [ForeignKey(nameof(CategoryId))]
        public virtual KnowledgeCategory Category { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string ScenarioCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string ScenarioName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? BusinessGoal { get; set; }

        [MaxLength(3000)]
        public string? Description { get; set; }

        public int Priority { get; set; }

        public int CooldownDays { get; set; }

        public double ConfidenceThreshold { get; set; }

        public bool Enabled { get; set; } = true;
        [MaxLength(50)]
        public string? ModelVersion { get; set; }
        public bool IsSystemScenario { get; set; } = false;
        public DateTime? LastTriggeredAt { get; set; }
        public int TriggerCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ScenarioCondition> Conditions { get; set; }
            = new List<ScenarioCondition>();

        public virtual ICollection<ScenarioExclusion> Exclusions { get; set; }
            = new List<ScenarioExclusion>();

        public virtual ICollection<ScenarioAction> Actions { get; set; }
            = new List<ScenarioAction>();

        public virtual ICollection<ScenarioMessageTemplate> MessageTemplates { get; set; }
            = new List<ScenarioMessageTemplate>();

        public virtual ScenarioExplanation? Explanation { get; set; }
    }
}