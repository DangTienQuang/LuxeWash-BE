using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using BLL.Services.Interface;
using AutoWashPro.BLL.Exceptions;

namespace BLL.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("File is empty.");
            }

            if (file.Length > 10485760) // 10MB
            {
                throw new BadRequestException($"File size too large. Got {file.Length}. Maximum is 10485760.");
            }

            using var stream = file.OpenReadStream();

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
                throw new BadRequestException(result.Error.Message);

            return result.SecureUrl.ToString();
        }
    }
}