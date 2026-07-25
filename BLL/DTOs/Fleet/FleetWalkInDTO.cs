using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs.Fleet
{
    public class FleetWalkInDTO
    {
        public string LicensePlate { get; set; } = null!;
        public int BranchId { get; set; }
    }
}
