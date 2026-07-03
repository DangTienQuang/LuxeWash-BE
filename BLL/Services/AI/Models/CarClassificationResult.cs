using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Models
{
    public class CarClassificationResult
    {
        public string ClassName { get; set; } = string.Empty; // e.g. "Toyota_Vios"
        public float Confidence { get; set; }
    }
}
