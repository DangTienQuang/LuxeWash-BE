using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AutoWashPro.BLL.DTOs
{
    public class UpdateBookingStatusDTO
    {
        [Required]
        [RegularExpression("^(Processing|Completed)$", ErrorMessage = "Status must be Processing or Completed.")]
        public string Status { get; set; } = null!;
        
        public IFormFile? CheckOutImage { get; set; }
    }
}
