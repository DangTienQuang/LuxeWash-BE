using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace AutoWashPro.BLL.DTOs
{
    public class CheckOutRequestDTO
    {
        [Required(ErrorMessage = "Check-out image is required.")]
        public IFormFile CheckOutImage { get; set; } = null!;
    }
}
