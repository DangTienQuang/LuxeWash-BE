using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Entities;
using System;

namespace AutoWashPro.Tests
{
    public class UserServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_User_" + Guid.NewGuid().ToString())
                .Options;
            
            _dbContext = new AutoWashDbContext(options);
            _userService = new UserService(_dbContext);
        }

        [Fact]
        public async Task GetProfileAsync_WithValidUserId_ShouldReturnProfile_TC40()
        {
            // Arrange
            var user = new User
            {
                UserId = 100,
                PhoneNumber = "0987654321",
                PasswordHash = "hashedpwd",
                Role = "Customer",
                Status = "Active",
                CustomerProfile = new CustomerProfile
                {
                    FullName = "John Doe"
                }
            };
            
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _userService.GetProfileAsync(100);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.UserId);
            Assert.Equal("0987654321", result.PhoneNumber);
            Assert.Equal("John Doe", result.FullName);
        }

        [Fact]
        public async Task GetProfileAsync_WithInvalidUserId_ShouldThrowNotFoundException_TC41()
        {
            // Arrange
            var nonExistentUserId = 999;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetProfileAsync(nonExistentUserId));
            Assert.Contains("User not found", ex.Message);
        }
    }
}
