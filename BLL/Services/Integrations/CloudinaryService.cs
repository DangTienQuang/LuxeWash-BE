using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using BLL.Services.Interface;
using AutoWashPro.BLL.Exceptions;
using System.IO;
using System.Linq;

namespace BLL.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

        private readonly Cloudinary _cloudinary;

        public CloudinaryService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder, bool imageOnly = true)
        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("File is empty.");
            }

            if (file.Length > 10485760) // 10MB
            {
                throw new BadRequestException($"File size too large. Got {file.Length}. Maximum is 10485760.");
            }

            // imageOnly = false: callers uploading already-validated documents (e.g. fleet import .xlsx).
            if (imageOnly)
            {
                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension.ToLowerInvariant()))
                {
                    throw new BadRequestException("Invalid file type. Only jpg, jpeg, png, and webp images are allowed.");
                }

                if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    throw new BadRequestException("Invalid file type. Only jpg, jpeg, png, and webp images are allowed.");
                }
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