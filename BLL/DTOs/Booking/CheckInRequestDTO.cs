using Microsoft.AspNetCore.Http;

namespace AutoWashPro.BLL.DTOs
{
    public class CheckInRequestDTO
    {
        public IFormFile? CheckInImage { get; set; }
    }
}
