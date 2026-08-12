#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Fleet
{
    public class FleetImportResultDTO
    {
        public int FleetImportBatchId { get; set; }
        public int TotalRows { get; set; }
        public int SuccessRows { get; set; }
        public int FailedRows { get; set; }
        public int ApprovedRows { get; set; }
        public int PendingApprovalRows { get; set; }
        public string Status { get; set; } = null;
        public List<FleetImportErrorDTO> Errors { get; set; } = new();
        public List<FleetImportVehicleResultDTO> Vehicles { get; set; } = new();
    }

    public class FleetImportVehicleResultDTO
    {
        public int RowNumber { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Message { get; set; } = null!;
    }
}
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
