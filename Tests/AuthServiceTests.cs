using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.Tests
{
    public class AuthServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<AutoWashPro.BLL.Services.IEmailService> _emailServiceMock;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + System.Guid.NewGuid().ToString())
                .Options;
            
            _dbContext = new AutoWashDbContext(options);
            _configMock = new Mock<IConfiguration>();
            _emailServiceMock = new Mock<AutoWashPro.BLL.Services.IEmailService>();
            
            _authService = new AuthService(_dbContext, _configMock.Object, _emailServiceMock.Object);
        }

        [Fact]
        public async Task LoginAsync_WithEmptyPhone_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var request = new LoginDTO { PhoneOrEmail = "", Password = "password" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Incorrect phone number/email", ex.Message);
        }
    }
}
