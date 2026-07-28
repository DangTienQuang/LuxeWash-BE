using BLL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class CloudinaryServiceTests
    {
        [Fact]
        public async Task UploadFileAsync_NullFile_ThrowsException()
        {
            var sut = new CloudinaryService(null!); // never reached, since the guard fires first

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.UploadFileAsync(null, "folder"));
            Assert.Equal("File is empty.", ex.Message);
        }

        [Fact]
        public async Task UploadFileAsync_EmptyFile_ThrowsException()
        {
            var sut = new CloudinaryService(null!);
            var emptyStream = new MemoryStream();
            var emptyFile = new FormFile(emptyStream, 0, 0, "file", "empty.png");

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.UploadFileAsync(emptyFile, "folder"));
            Assert.Equal("File is empty.", ex.Message);
        }
    }
}