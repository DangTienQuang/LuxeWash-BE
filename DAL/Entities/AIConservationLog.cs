using AutoWashPro.DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class AIConversationLog
    {
        [Key]
        public int ConversationLogId { get; set; }
        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }
        [MaxLength(300)]
        public string Message { get; set; }
        [MaxLength(2000)]
        public string Response { get; set; }

        public bool Blocked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
