using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class IncidentCaseCause
    {
        public long CaseId { get; set; }
        
        [ForeignKey("CaseId")]
        public virtual IncidentAffectedBooking Case { get; set; } = null!;

        public long IncidentId { get; set; }
        
        [ForeignKey("IncidentId")]
        public virtual BranchIncident Incident { get; set; } = null!;
    }
}
