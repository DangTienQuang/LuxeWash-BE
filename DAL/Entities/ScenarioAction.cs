using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class ScenarioAction
    {
        [Key]
        public int ActionId { get; set; }
        public int ScenarioId { get; set; }
        public int VoucherId { get; set; }

        [ForeignKey(nameof(ScenarioId))]
        public virtual KnowledgeScenario Scenario { get; set; } = null!;

        [ForeignKey(nameof(VoucherId))]
        public virtual Voucher Voucher { get; set; } = null!;
        public int Weight { get; set; }
        public int CooldownDays { get; set; }
        public double ExpectedConversion { get; set; }
        public decimal ExpectedRevenue { get; set; }
        public bool IsPrimary { get; set; }
        public int Priority { get; set; }
        public int MaxPerCustomer { get; set; }
        public bool AllowStacking { get; set; }
        public bool StopProcessing { get; set; }
    }
}