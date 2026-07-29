using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    [Table("OutboxMessages")]
    public class OutboxMessage
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Type { get; set; } = null!;

        [Required]
        public string Payload { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }
        
        public string? ErrorMessage { get; set; }

        public int RetryCount { get; set; } = 0;

        public DateTime? NextRetryAt { get; set; }
    }
}
