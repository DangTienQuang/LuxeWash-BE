using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs
{
    public class AIChatRequestDTO
    {
        [MaxLength(300)]
        public string Message { get; set; } = string.Empty;
    }
}
