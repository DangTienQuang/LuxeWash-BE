using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Constants;

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
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            
            _dbContext = new AutoWashDbContext(options);
            _configMock = new Mock<IConfiguration>();

            _configMock.Setup(c => c["Jwt:Key"]).Returns("ThisIsAVerySecretKeyForTestingPurposesOnly!");
            _configMock.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
            _configMock.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
            _configMock.Setup(c => c["Jwt:AccessTokenExpirationMinutes"]).Returns("60");
            _configMock.Setup(c => c["Jwt:RefreshTokenExpirationDays"]).Returns("7");

            _emailServiceMock = new Mock<AutoWashPro.BLL.Services.IEmailService>();
            
            _authService = new AuthService(_dbContext, _configMock.Object, _emailServiceMock.Object);
        }

        [Fact]
        public async Task RegisterAsync_ValidPayload_CreatesUser_ReturnsPendingStatus()
        {
            var request = new RegisterDTO 
            { 
                PhoneNumber = "0901234567", 
                Email = "test@example.com", 
                Password = "Password123!", 
                FullName = "Test User" 
            };

            var result = await _authService.RegisterAsync(request);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Email));
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == "0901234567");
            Assert.NotNull(user);
            Assert.Equal(UserStatuses.Pending, user.Status);
        }

        [Fact]
        public async Task RegisterAsync_DuplicatePhone_ThrowsBadRequestException()
        {
            _dbContext.Users.Add(new User { PhoneNumber = "0901234567", Email = "old@test.com", PasswordHash = "hash", Role = UserRoles.Customer, Status = UserStatuses.Active });
            await _dbContext.SaveChangesAsync();

            var request = new RegisterDTO { PhoneNumber = "0901234567", Email = "new@test.com", Password = "Password123!", FullName = "Test" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
            Assert.Contains("already registered", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ReturnsTokens()
        {
            var user = new User 
            { 
                PhoneNumber = "0909999999", 
                Email = "login@test.com", 
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123"), 
                Role = UserRoles.Customer, 
                Status = UserStatuses.Active 
            };
            _dbContext.Users.Add(user);
            _dbContext.CustomerProfiles.Add(new CustomerProfile { ProfileId = 1, UserId = user.UserId, FullName = "Login Test" });
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0909999999", Password = "ValidPassword123" };

            var result = await _authService.LoginAsync(request);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task LoginAsync_IncorrectPassword_ThrowsUnauthorizedException()
        {
            var user = new User { PhoneNumber = "0908888888", PasswordHash = BCrypt.Net.BCrypt.HashPassword("RightPassword"), Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0908888888", Password = "WrongPassword" };

            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Incorrect", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_WithEmptyPhone_ShouldThrowUnauthorizedException()
        {
            var request = new LoginDTO { PhoneOrEmail = "", Password = "password" };
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Incorrect phone number/email", ex.Message);
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidCurrentPassword_ChangesSuccessfully()
        {
            var user = new User { UserId = 10, PhoneNumber = "0907777777", PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword"), Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ChangePasswordDTO { OldPassword = "OldPassword", NewPassword = "NewPassword123" };

            var result = await _authService.ChangePasswordAsync(10, request);

            Assert.True(result);
            var updatedUser = await _dbContext.Users.FindAsync(10);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123", updatedUser.PasswordHash));
        }

        [Fact]
        public async Task ChangePasswordAsync_IncorrectCurrentPassword_ThrowsBadRequestException()
        {
            var user = new User { UserId = 20, PhoneNumber = "0906666666", PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword"), Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ChangePasswordDTO { OldPassword = "WrongPassword", NewPassword = "NewPassword123" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.ChangePasswordAsync(20, request));
            Assert.Contains("old password", ex.Message.ToLower());
        }

        [Fact]
        public async Task LogoutAsync_ValidRequest_InvalidatesRefreshToken()
        {
            var user = new User { UserId = 30, PhoneNumber = "0905555555", RefreshToken = "SomeValidToken", Role = UserRoles.Customer, Status = UserStatuses.Active, PasswordHash = "hash" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            await _authService.LogoutAsync(30);

            var updatedUser = await _dbContext.Users.FindAsync(30);
            Assert.Null(updatedUser.RefreshToken);
        }
    }
}
