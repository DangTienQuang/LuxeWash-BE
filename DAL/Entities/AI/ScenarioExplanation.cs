using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entities.AI
{
    public class ScenarioExplanation
    {
        [Key, ForeignKey(nameof(Scenario))]
        public int ScenarioId { get; set; }

        public virtual KnowledgeScenario Scenario { get; set; } = null!;

        public string Reasoning { get; set; } = string.Empty;

        public string? BusinessContext { get; set; }

        public string? ExpectedOutcome { get; set; }

        public string? LLMNotes { get; set; }
    }
}