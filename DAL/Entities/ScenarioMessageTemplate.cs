using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ScenarioMessageTemplate
    {
        [Key]
        public int TemplateId { get; set; }

        public int ScenarioId { get; set; }

        [ForeignKey(nameof(ScenarioId))]
        public virtual KnowledgeScenario Scenario { get; set; } = null!;

        [MaxLength(100)]
        public string Language { get; set; } = "en";

        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        public string PushMessage { get; set; } = string.Empty;

        public string? SmsMessage { get; set; }

        public string? EmailMessage { get; set; }
    }
}