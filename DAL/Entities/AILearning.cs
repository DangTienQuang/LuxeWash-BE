using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class AILearning
    {
        [Key]
        public int LearningId { get; set; }

        public int ScenarioId { get; set; }

        public virtual KnowledgeScenario Scenario { get; set; } = null!;

        public int? VoucherId { get; set; }

        public virtual Voucher? Voucher { get; set; }

        public int TimesTriggered { get; set; }

        public int NotificationsSent { get; set; }

        public int NotificationsOpened { get; set; }

        public int ClickedCount { get; set; }

        public int AcceptedCount { get; set; }

        public int RedeemedCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageRevenue { get; set; }

        public double AverageConfidence { get; set; }

        public double SuccessRate { get; set; }

        public double RedemptionRate { get; set; }

        public double ClickThroughRate { get; set; }

        public double AcceptanceRate { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}