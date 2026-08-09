using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class FleetImportDetailDTO
    {
        public int FleetImportBatchId { get; set; }
        public string Status { get; set; } = null!;
        public int TotalRows { get; set; }
        public int SuccessRows { get; set; }
        public int FailedRows { get; set; }
        public List<FleetImportErrorDTO> Errors { get; set; } = new();
    }
}
