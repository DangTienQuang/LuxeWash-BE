using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.DAL.Entities
{
    public class KnowledgeCategory
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int Priority { get; set; }

        public virtual ICollection<KnowledgeScenario> Scenarios { get; set; }
            = new List<KnowledgeScenario>();
    }
}