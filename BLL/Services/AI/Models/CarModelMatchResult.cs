using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Models
{
    public class CarModelMatchResult
    {
        public int? CarModelId { get; set; }
        public string Status { get; set; } = "Pending"; // mirrors CarModel.Status
        public bool IsNewlyCreated { get; set; }
        public int? VehicleTypeId { get; set; }
        public string? VehicleTypeName { get; set; }
    }
}
