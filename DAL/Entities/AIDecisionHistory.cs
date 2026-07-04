using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class AIDecisionHistory
    {
        [Key]
        public int DecisionId { get; set; }

        public int CustomerId { get; set; }

        public virtual User Customer { get; set; } = null!;

        public int ScenarioId { get; set; }

        public virtual KnowledgeScenario Scenario { get; set; } = null!;

        public int? VoucherId { get; set; }

        public virtual Voucher? Voucher { get; set; }

        public int? ServiceId { get; set; }

        public virtual Service? Service { get; set; }

        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        public double Confidence { get; set; }

        public int FinalScore { get; set; }

        public string DecisionReason { get; set; } = string.Empty;

        public string? GeneratedPrompt { get; set; }

        public string? LLMResponse { get; set; }

        public bool NotificationSent { get; set; }

        public bool CustomerOpened { get; set; }

        public bool CustomerClicked { get; set; }

        public bool Accepted { get; set; }

        public bool Redeemed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RevenueGenerated { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedRevenue { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RedeemedAt { get; set; }
    }
}