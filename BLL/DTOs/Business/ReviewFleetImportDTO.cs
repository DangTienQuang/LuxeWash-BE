using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class ReviewFleetImportDTO
    {
        public int FleetImportBatchId { get; set; }
        public bool Approved { get; set; }
        public string? RejectionReason { get; set; }
    }
}
