using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
namespace AutoWashPro.BLL.DTOs
{
    public class CheckInRequestDTO
    {
        [Required(ErrorMessage = "Check-in image is required.")]
        public IFormFile CheckInImage { get; set; } = null!;
    }
}
