using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.Tests
{
    public class UserServiceTests
    {
        private readonly AutoWashDbContext _context;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new AutoWashDbContext(options);

            _userService = new UserService(_context);
        }

        private async Task SeedUsersAsync(int primaryUserId, int secondaryUserId)
        {
            _context.Users.Add(new User 
            { 
                UserId = primaryUserId, 
                Email = "user1@test.com", 
                PhoneNumber = "0901111111", 
                PasswordHash = "hash", 
                Role = "Customer", 
                Status = "Active" 
            });
            _context.CustomerProfiles.Add(new CustomerProfile 
            { 
                ProfileId = primaryUserId, 
                UserId = primaryUserId, 
                FullName = "User One",
                TotalPoint = 0
            });

            _context.Users.Add(new User 
            { 
                UserId = secondaryUserId, 
                Email = "user2@test.com", 
                PhoneNumber = "0902222222", 
                PasswordHash = "hash", 
                Role = "Customer", 
                Status = "Active" 
            });
            _context.CustomerProfiles.Add(new CustomerProfile 
            { 
                ProfileId = secondaryUserId, 
                UserId = secondaryUserId, 
                FullName = "User Two",
                TotalPoint = 0
            });

            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateProfileAsync_ValidData_UpdatesSuccessfully_TC1()
        {
            // Arrange
            int userId = 10;
            await SeedUsersAsync(userId, 20);

            var request = new UpdateUserProfileDTO
            {
                FullName = "Updated User One",
                PhoneNumber = "0903333333",
                Email = "newuser1@test.com",
                DateOfBirth = new DateTime(1990, 1, 1)
            };

            // Act
            var result = await _userService.UpdateProfileAsync(userId, request);

            // Assert
            Assert.True(result);
            var updatedUser = await _context.Users.Include(u => u.CustomerProfile).FirstOrDefaultAsync(u => u.UserId == userId);
            
            Assert.NotNull(updatedUser);
            Assert.Equal("newuser1@test.com", updatedUser.Email);
            Assert.Equal("0903333333", updatedUser.PhoneNumber);
            Assert.Equal("Updated User One", updatedUser.CustomerProfile.FullName);
            Assert.Equal(new DateTime(1990, 1, 1), updatedUser.CustomerProfile.DateOfBirth);
        }

        [Fact]
        public async Task UpdateProfileAsync_DuplicateEmail_ThrowsException_TC2()
        {
            // Arrange
            int userId1 = 30;
            int userId2 = 40;
            await SeedUsersAsync(userId1, userId2);

            var request = new UpdateUserProfileDTO
            {
                Email = "user2@test.com" // This email belongs to userId2
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => _userService.UpdateProfileAsync(userId1, request));
            Assert.Contains("already used", exception.Message);
        }

        [Fact]
        public async Task UpdateProfileAsync_DuplicatePhone_ThrowsException_TC3()
        {
            // Arrange
            int userId1 = 50;
            int userId2 = 60;
            await SeedUsersAsync(userId1, userId2);

            var request = new UpdateUserProfileDTO
            {
                PhoneNumber = "0902222222" // This phone belongs to userId2
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => _userService.UpdateProfileAsync(userId1, request));
            Assert.Contains("already used", exception.Message);
        }

        [Fact]
        public async Task UpdateProfileAsync_DateOfBirthAlreadySet_ThrowsException_TC4()
        {
            // Arrange
            int userId = 70;
            await SeedUsersAsync(userId, 80);
            
            var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            profile.DateOfBirth = new DateTime(1990, 1, 1);
            await _context.SaveChangesAsync();

            var request = new UpdateUserProfileDTO
            {
                DateOfBirth = new DateTime(1995, 1, 1) // Attempting to change an already set DOB
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<BadRequestException>(() => _userService.UpdateProfileAsync(userId, request));
            Assert.Contains("cannot change your birth date", exception.Message);
        }
    }
}
