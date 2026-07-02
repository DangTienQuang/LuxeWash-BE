using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class AIAuditLog
    {
        [Key]
        public int AuditId { get; set; }

        public int CustomerId { get; set; }

        public virtual User Customer { get; set; } = null!;

        [MaxLength(100)]
        public string EngineVersion { get; set; } = "RuleEngine-v1";

        [MaxLength(100)]
        public string? ModelVersion { get; set; }

        public string MatchedScenarios { get; set; } = string.Empty;
        // JSON array of scenario IDs

        public int SelectedScenarioId { get; set; }

        public int DecisionScore { get; set; }

        public double Confidence { get; set; }

        public string DecisionReason { get; set; } = string.Empty;

        public string? Prompt { get; set; }

        public string? AIResponse { get; set; }

        public long ExecutionTimeMs { get; set; }

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}