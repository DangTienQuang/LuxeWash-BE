using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs
{
    public class AIChatResponseDTO
    {
        public string Reply { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
    }
}
