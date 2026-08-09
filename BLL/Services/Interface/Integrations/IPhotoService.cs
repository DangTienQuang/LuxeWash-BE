using Microsoft.AspNetCore.Http;
namespace BLL.Services.Interface
{
    public interface IPhotoService
    {
        Task<string> UploadImageAsync(IFormFile file);
    }
}