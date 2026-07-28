using BLL.Helpers;
using BLL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests.BLL
{
    public class PhotoServiceTests
    {
        [Fact]
        public async Task UploadImageAsync_EmptyFile_ThrowsNullReferenceException()
        {
            var settings = Options.Create(new CloudinarySettings { CloudName = "test", ApiKey = "test", ApiSecret = "test" });
            var sut = new PhotoService(settings);

            var emptyStream = new MemoryStream();
            var emptyFile = new FormFile(emptyStream, 0, 0, "file", "empty.png");

            // Documents actual current behavior: skipping upload for an empty file
            // leaves uploadResult.SecureUrl null, and .ToString() on it throws NRE
            // rather than a handled validation error. Worth flagging to the team —
            // this looks like an unintentional bug, not a designed guard clause.
            await Assert.ThrowsAsync<NullReferenceException>(() => sut.UploadImageAsync(emptyFile));
        }
    }
}