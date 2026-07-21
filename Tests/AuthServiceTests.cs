using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Entities;
using AutoWashPro.BLL.Constants;
using System;

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
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + Guid.NewGuid().ToString())
                .Options;
            
            _dbContext = new AutoWashDbContext(options);
            _configMock = new Mock<IConfiguration>();
            _emailServiceMock = new Mock<AutoWashPro.BLL.Services.IEmailService>();
            
            // Setup mock config for JWT
            _configMock.Setup(c => c["Jwt:Key"]).Returns("ThisIsAVerySecretKeyForJwtTesting12345");
            _configMock.Setup(c => c["Jwt:Issuer"]).Returns("SmartWashIssuer");
            _configMock.Setup(c => c["Jwt:Audience"]).Returns("SmartWashAudience");
            _configMock.Setup(c => c["Jwt:ExpireDays"]).Returns("7");

            _authService = new AuthService(_dbContext, _configMock.Object, _emailServiceMock.Object);
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ShouldReturnToken_TC11()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0901234567",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Active
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0901234567", Password = "CorrectPassword123" };

            // Act
            var result = await _authService.LoginAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal("0901234567", result.PhoneNumber);
        }

        [Fact]
        public async Task LoginAsync_WithWrongPassword_ShouldThrowUnauthorizedException_TC13()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0901234567",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Active
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0901234567", Password = "WrongPassword!" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Incorrect phone number/email or password", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_WithPendingAccount_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0901234567",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Pending
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0901234567", Password = "password" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Account email not verified", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_WithInactiveAccount_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0901234567",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Blocked
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0901234567", Password = "password" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Account is locked or inactive", ex.Message);
        }
    }
}
