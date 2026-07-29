using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        public int UserId { get; set; }

        public string Action { get; set; } = null!;

        public string EntityName { get; set; } = null!;

        public string EntityId { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
