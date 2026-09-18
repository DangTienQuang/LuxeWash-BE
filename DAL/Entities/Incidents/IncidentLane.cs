using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class IncidentLane
    {
        public long IncidentId { get; set; }
        
        [ForeignKey("IncidentId")]
        public virtual BranchIncident Incident { get; set; } = null!;

        public int LaneId { get; set; }
        
        [ForeignKey("LaneId")]
        public virtual Lane Lane { get; set; } = null!;
    }
}
