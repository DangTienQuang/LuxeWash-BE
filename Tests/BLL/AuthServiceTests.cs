using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace AutoWashPro.Tests
{
    public class AuthServiceTests
    {
        private readonly AutoWashDbContext _dbContext;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<AutoWashPro.BLL.Services.IEmailService> _emailServiceMock;
        private readonly AuthService _authService;

        private string CreateTestJwt(int? userId, string phoneNumber, string role, DateTime expires, bool includeUserIdClaim = true)
        {
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var key = System.Text.Encoding.ASCII.GetBytes("this-is-a-test-secret-key-32-chars-minimum");

            var claims = new List<System.Security.Claims.Claim>();
            if (includeUserIdClaim)
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId?.ToString() ?? string.Empty));
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.MobilePhone, phoneNumber));
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));

            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(claims),
                NotBefore = expires.AddMinutes(-10), // must be before Expires, even when Expires is in the past
                Expires = expires,
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<AutoWashDbContext>()
                .UseInMemoryDatabase(databaseName: "SmartWashTestDb_" + System.Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _dbContext = new AutoWashDbContext(options);
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["Jwt:Key"]).Returns("this-is-a-test-secret-key-32-chars-minimum");
            _emailServiceMock = new Mock<AutoWashPro.BLL.Services.IEmailService>();
            _emailServiceMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _authService = new AuthService(_dbContext, _configMock.Object, _emailServiceMock.Object);
        }

        [Fact]
        public async Task LoginAsync_UserNotFound_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var request = new LoginDTO { PhoneOrEmail = "0900000000", Password = "password" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("Incorrect phone number/email or password", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_WrongPassword_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0900000001",
                Email = "user1@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Active
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0900000001", Password = "wrong-password" };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
        }

        [Fact]
        public async Task LoginAsync_PendingAccount_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0900000002",
                Email = "user2@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Pending
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0900000002", Password = "password" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("not verified", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_InactiveAccount_ShouldThrowUnauthorizedException()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0900000003",
                Email = "user3@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = UserRoles.Customer,
                Status = "Locked"
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "0900000003", Password = "password" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            Assert.Contains("locked or inactive", ex.Message);
        }

        [Fact]
        public async Task LoginAsync_ValidCredentials_ShouldReturnAuthResponseWithToken()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0900000004",
                Email = "user4@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                CustomerProfile = new CustomerProfile { FullName = "Nguyen Van A" }
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new LoginDTO { PhoneOrEmail = "user4@test.com", Password = "password" };

            // Act
            var result = await _authService.LoginAsync(request);

            // Assert
            Assert.NotNull(result.Token);
            Assert.NotNull(result.RefreshToken);
            Assert.Equal("Nguyen Van A", result.FullName);
            Assert.Equal(UserRoles.Customer, result.Role);
        }

        [Fact]
        public async Task VerifyOtpAsync_UserNotFound_ShouldThrowNotFoundException()
        {
            var request = new VerifyOtpDTO { Email = "ghost@test.com", Otp = "123456" };
            await Assert.ThrowsAsync<NotFoundException>(() => _authService.VerifyOtpAsync(request));
        }

        [Fact]
        public async Task VerifyOtpAsync_AccountNotPending_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0911", Email = "active@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new VerifyOtpDTO { Email = "active@test.com", Otp = "123456" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.VerifyOtpAsync(request));
        }

        [Fact]
        public async Task VerifyOtpAsync_NoOtpOnRecord_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0912", Email = "noOtp@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Pending };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new VerifyOtpDTO { Email = "noOtp@test.com", Otp = "123456" };
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.VerifyOtpAsync(request));
            Assert.Contains("does not have a verification OTP", ex.Message);
        }

        [Fact]
        public async Task VerifyOtpAsync_ExpiredOtp_ShouldThrowBadRequestException()
        {
            var user = new User
            {
                PhoneNumber = "0913",
                Email = "expired@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Pending,
                EmailVerificationOtpHash = "somehash",
                EmailVerificationOtpExpiresAt = DateTime.UtcNow.AddMinutes(-5) // already expired
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new VerifyOtpDTO { Email = "expired@test.com", Otp = "123456" };
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.VerifyOtpAsync(request));
            Assert.Contains("expired", ex.Message);
        }

        [Fact]
        public async Task VerifyOtpAsync_WrongOtp_ShouldThrowBadRequestException()
        {
            var user = new User
            {
                PhoneNumber = "0914",
                Email = "wrongotp@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Pending,
                EmailVerificationOtpHash = "correcthash",
                EmailVerificationOtpExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new VerifyOtpDTO { Email = "wrongotp@test.com", Otp = "999999" };
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.VerifyOtpAsync(request));
            Assert.Contains("Incorrect OTP", ex.Message);
        }

        [Fact]
        public async Task VerifyOtpAsync_CorrectOtp_ShouldActivateAccountAndReturnToken()
        {
            // Arrange — hash must match the private HashOtp logic: SHA256 -> hex string
            var otp = "123456";
            var otpHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(otp)));

            var user = new User
            {
                PhoneNumber = "0915",
                Email = "verify@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Pending,
                EmailVerificationOtpHash = otpHash,
                EmailVerificationOtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CustomerProfile = new CustomerProfile { FullName = "Tran Thi B" }
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new VerifyOtpDTO { Email = "verify@test.com", Otp = otp };

            // Act
            var result = await _authService.VerifyOtpAsync(request);

            // Assert
            Assert.NotNull(result.Token);
            Assert.Equal(UserStatuses.Active, user.Status);
            Assert.Null(user.EmailVerificationOtpHash);
        }

        [Fact]
        public async Task LogoutAsync_UserNotFound_ShouldThrowNotFoundException()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _authService.LogoutAsync(9999));
        }

        [Fact]
        public async Task LogoutAsync_ValidUser_ShouldClearRefreshToken()
        {
            // Arrange
            var user = new User
            {
                PhoneNumber = "0920",
                Email = "logout@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                RefreshToken = "some-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Act
            await _authService.LogoutAsync(user.UserId);

            // Assert
            Assert.Null(user.RefreshToken);
            Assert.Null(user.RefreshTokenExpiryTime);
        }

        [Fact]
        public async Task ChangePasswordAsync_SameOldAndNewPassword_ShouldThrowBadRequestException()
        {
            var request = new ChangePasswordDTO { OldPassword = "same123", NewPassword = "same123" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ChangePasswordAsync(1, request));
        }

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ShouldThrowNotFoundException()
        {
            var request = new ChangePasswordDTO { OldPassword = "old123", NewPassword = "new123" };
            await Assert.ThrowsAsync<NotFoundException>(() => _authService.ChangePasswordAsync(9999, request));
        }

        [Fact]
        public async Task ChangePasswordAsync_WrongOldPassword_ShouldThrowBadRequestException()
        {
            var user = new User
            {
                PhoneNumber = "0921",
                Email = "changepw@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-old"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Active
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ChangePasswordDTO { OldPassword = "wrong-old", NewPassword = "new123" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ChangePasswordAsync(user.UserId, request));
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_ShouldReturnTrueAndUpdateHash()
        {
            var user = new User
            {
                PhoneNumber = "0922",
                Email = "changepw2@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-old"),
                Role = UserRoles.Customer,
                Status = UserStatuses.Active
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ChangePasswordDTO { OldPassword = "correct-old", NewPassword = "brand-new-pw" };

            var result = await _authService.ChangePasswordAsync(user.UserId, request);

            Assert.True(result);
            Assert.True(BCrypt.Net.BCrypt.Verify("brand-new-pw", user.PasswordHash));
        }

        [Fact]
        public async Task ForgotPasswordAsync_UserNotFound_ShouldThrowNotFoundException()
        {
            var request = new ForgotPasswordDTO { Email = "ghost@test.com" };
            await Assert.ThrowsAsync<NotFoundException>(() => _authService.ForgotPasswordAsync(request));
        }

        [Fact]
        public async Task ForgotPasswordAsync_InactiveAccount_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0923", Email = "inactive@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = "Locked" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ForgotPasswordDTO { Email = "inactive@test.com" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ForgotPasswordAsync(request));
        }

        [Fact]
        public async Task ForgotPasswordAsync_EmailSendFails_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0924", Email = "fail@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _emailServiceMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP down"));

            var request = new ForgotPasswordDTO { Email = "fail@test.com" };
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.ForgotPasswordAsync(request));
            Assert.Contains("SMTP down", ex.Message);
        }

        [Fact]
        public async Task ForgotPasswordAsync_ValidAccount_ShouldSetOtpAndSendEmail()
        {
            var user = new User { PhoneNumber = "0925", Email = "valid@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _emailServiceMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var request = new ForgotPasswordDTO { Email = "valid@test.com" };
            await _authService.ForgotPasswordAsync(request);

            Assert.NotNull(user.EmailVerificationOtpHash);
            _emailServiceMock.Verify(e => e.SendEmailAsync("valid@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_MalformedAccessToken_ShouldThrow()
        {
            var request = new RefreshTokenDTO { AccessToken = "not-a-real-jwt", RefreshToken = "whatever" };

            // Malformed tokens fail signature validation before reaching our own checks —
            // the JWT library throws its own exception type here, not UnauthorizedException.
            await Assert.ThrowsAnyAsync<Exception>(() => _authService.RefreshTokenAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_MissingUserIdClaim_ShouldThrowUnauthorizedException()
        {
            var expiredToken = CreateTestJwt(null, "0930", UserRoles.Customer, DateTime.UtcNow.AddMinutes(-5), includeUserIdClaim: false);
            var request = new RefreshTokenDTO { AccessToken = expiredToken, RefreshToken = "any" };

            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request));
            Assert.Contains("does not contain User information", ex.Message);
        }

        [Fact]
        public async Task RefreshTokenAsync_RefreshTokenMismatch_ShouldThrowUnauthorizedException()
        {
            var user = new User
            {
                PhoneNumber = "0931",
                Email = "refresh1@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                RefreshToken = "stored-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var expiredToken = CreateTestJwt(user.UserId, user.PhoneNumber, user.Role, DateTime.UtcNow.AddMinutes(-5));
            var request = new RefreshTokenDTO { AccessToken = expiredToken, RefreshToken = "wrong-token" };

            await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_ExpiredRefreshToken_ShouldThrowUnauthorizedException()
        {
            var user = new User
            {
                PhoneNumber = "0932",
                Email = "refresh2@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                RefreshToken = "stored-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1) // already expired
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var expiredToken = CreateTestJwt(user.UserId, user.PhoneNumber, user.Role, DateTime.UtcNow.AddMinutes(-5));
            var request = new RefreshTokenDTO { AccessToken = expiredToken, RefreshToken = "stored-token" };

            await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.RefreshTokenAsync(request));
        }

        [Fact]
        public async Task RefreshTokenAsync_ValidRequest_ShouldReturnNewTokens()
        {
            var user = new User
            {
                PhoneNumber = "0933",
                Email = "refresh3@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                RefreshToken = "stored-token",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7),
                CustomerProfile = new CustomerProfile { FullName = "Le Van C" }
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var expiredToken = CreateTestJwt(user.UserId, user.PhoneNumber, user.Role, DateTime.UtcNow.AddMinutes(-5));
            var request = new RefreshTokenDTO { AccessToken = expiredToken, RefreshToken = "stored-token" };

            var result = await _authService.RefreshTokenAsync(request);

            Assert.NotNull(result.Token);
            Assert.NotEqual("stored-token", result.RefreshToken);
            Assert.Equal("Le Van C", result.FullName);
        }

        [Fact]
        public async Task ResendOtpAsync_UserNotFound_ShouldThrowNotFoundException()
        {
            var request = new ResendOtpDTO { Email = "ghost@test.com" };
            await Assert.ThrowsAsync<NotFoundException>(() => _authService.ResendOtpAsync(request));
        }

        [Fact]
        public async Task ResendOtpAsync_AccountNotPending_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0940", Email = "notpending@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResendOtpDTO { Email = "notpending@test.com" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ResendOtpAsync(request));
        }

        [Fact]
        public async Task ResendOtpAsync_EmailSendFails_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0941", Email = "resendfail@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Pending };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _emailServiceMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP timeout"));

            var request = new ResendOtpDTO { Email = "resendfail@test.com" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ResendOtpAsync(request));
        }

        [Fact]
        public async Task ResendOtpAsync_ValidPendingAccount_ShouldSetNewOtpAndReturnResponse()
        {
            var user = new User { PhoneNumber = "0942", Email = "resend@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Pending };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResendOtpDTO { Email = "resend@test.com" };
            var result = await _authService.ResendOtpAsync(request);

            Assert.Equal(user.UserId, result.UserId);
            Assert.NotNull(user.EmailVerificationOtpHash);
        }

        [Fact]
        public async Task ResetPasswordAsync_UserNotFound_ShouldThrowNotFoundException()
        {
            var request = new ResetPasswordDTO { Email = "ghost@test.com", Otp = "123456", NewPassword = "new123" };
            await Assert.ThrowsAsync<NotFoundException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task ResetPasswordAsync_InactiveAccount_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0950", Email = "resetlocked@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = "Locked" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResetPasswordDTO { Email = "resetlocked@test.com", Otp = "123456", NewPassword = "new123" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task ResetPasswordAsync_NoOtpOnRecord_ShouldThrowBadRequestException()
        {
            var user = new User { PhoneNumber = "0951", Email = "resetnootp@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResetPasswordDTO { Email = "resetnootp@test.com", Otp = "123456", NewPassword = "new123" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task ResetPasswordAsync_ExpiredOtp_ShouldThrowBadRequestException()
        {
            var user = new User
            {
                PhoneNumber = "0952",
                Email = "resetexpired@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                EmailVerificationOtpHash = "somehash",
                EmailVerificationOtpExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResetPasswordDTO { Email = "resetexpired@test.com", Otp = "123456", NewPassword = "new123" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task ResetPasswordAsync_WrongOtp_ShouldThrowBadRequestException()
        {
            var user = new User
            {
                PhoneNumber = "0953",
                Email = "resetwrong@test.com",
                PasswordHash = "x",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                EmailVerificationOtpHash = "correcthash",
                EmailVerificationOtpExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResetPasswordDTO { Email = "resetwrong@test.com", Otp = "999999", NewPassword = "new123" };
            await Assert.ThrowsAsync<BadRequestException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task ResetPasswordAsync_ValidOtp_ShouldUpdatePasswordAndClearOtp()
        {
            var otp = "654321";
            var otpHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(otp)));

            var user = new User
            {
                PhoneNumber = "0954",
                Email = "resetok@test.com",
                PasswordHash = "old-hash",
                Role = UserRoles.Customer,
                Status = UserStatuses.Active,
                EmailVerificationOtpHash = otpHash,
                EmailVerificationOtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
                RefreshToken = "some-token"
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var request = new ResetPasswordDTO { Email = "resetok@test.com", Otp = otp, NewPassword = "brand-new-password" };
            await _authService.ResetPasswordAsync(request);

            Assert.True(BCrypt.Net.BCrypt.Verify("brand-new-password", user.PasswordHash));
            Assert.Null(user.EmailVerificationOtpHash);
            Assert.Null(user.RefreshToken);
        }

        [Fact]
        public async Task RegisterAsync_PhoneAlreadyRegistered_ShouldThrowBadRequestException()
        {
            var existing = new User { PhoneNumber = "0960000001", Email = "taken1@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(existing);
            await _dbContext.SaveChangesAsync();

            var request = new RegisterDTO { PhoneNumber = "0960000001", Email = "new@test.com", Password = "pw123456", FullName = "New Guy" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
            Assert.Contains("phone number is already registered", ex.Message);
        }

        [Fact]
        public async Task RegisterAsync_EmailAlreadyRegistered_ShouldThrowBadRequestException()
        {
            var existing = new User { PhoneNumber = "0960000002", Email = "taken2@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Active };
            _dbContext.Users.Add(existing);
            await _dbContext.SaveChangesAsync();

            var request = new RegisterDTO { PhoneNumber = "0960000099", Email = "taken2@test.com", Password = "pw123456", FullName = "New Guy" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
            Assert.Contains("email is already registered", ex.Message);
        }

        [Fact]
        public async Task RegisterAsync_EmailUsedByDifferentUser_ShouldThrowBadRequestException()
        {
            // A pending user with a different phone owns this email already
            var pendingOther = new User { PhoneNumber = "0960000003", Email = "shared@test.com", PasswordHash = "x", Role = UserRoles.Customer, Status = UserStatuses.Pending };
            _dbContext.Users.Add(pendingOther);
            await _dbContext.SaveChangesAsync();

            // New registration with a different phone number, same email
            var request = new RegisterDTO { PhoneNumber = "0960000004", Email = "shared@test.com", Password = "pw123456", FullName = "Someone Else" };

            await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
        }

        [Fact]
        public async Task RegisterAsync_NoDefaultTierExists_ShouldCreateStandardTier()
        {
            var request = new RegisterDTO { PhoneNumber = "0960000005", Email = "newtier@test.com", Password = "pw123456", FullName = "Tier Tester" };

            await _authService.RegisterAsync(request);

            var tier = await _dbContext.Tiers.FirstOrDefaultAsync(t => t.MinAccumulatedPoints == 0);
            Assert.NotNull(tier);
            Assert.Equal("Standard", tier.TierName);
        }

        [Fact]
        public async Task RegisterAsync_DefaultTierExists_ShouldNotCreateDuplicate()
        {
            _dbContext.Tiers.Add(new Tier { TierName = "Standard", PointMultiplier = 1.0, BookingWindowDays = 7, MinAccumulatedPoints = 0 });
            await _dbContext.SaveChangesAsync();

            var request = new RegisterDTO { PhoneNumber = "0960000006", Email = "existingtier@test.com", Password = "pw123456", FullName = "Tier Tester 2" };

            await _authService.RegisterAsync(request);

            var tierCount = await _dbContext.Tiers.CountAsync(t => t.MinAccumulatedPoints == 0);
            Assert.Equal(1, tierCount);
        }

        [Fact]
        public async Task RegisterAsync_ExistingPendingUser_SamePhoneAndEmail_ShouldUpdateInPlace()
        {
            var pending = new User
            {
                PhoneNumber = "0960000007",
                Email = "pending1@test.com",
                PasswordHash = "old-hash",
                Role = UserRoles.Customer,
                Status = UserStatuses.Pending,
                CustomerProfile = new CustomerProfile { FullName = "Old Name" }
            };
            _dbContext.Users.Add(pending);
            await _dbContext.SaveChangesAsync();
            var originalUserId = pending.UserId;

            var request = new RegisterDTO { PhoneNumber = "0960000007", Email = "pending1@test.com", Password = "newpw123", FullName = "Updated Name" };

            var result = await _authService.RegisterAsync(request);

            Assert.Equal(originalUserId, result.UserId); // same user, not a new one
            Assert.Equal("Updated Name", pending.CustomerProfile.FullName);
            var totalUsers = await _dbContext.Users.CountAsync();
            Assert.Equal(1, totalUsers);
        }

        [Fact]
        public async Task RegisterAsync_NewUser_ShouldCreateUserProfileAndWallet()
        {
            var request = new RegisterDTO { PhoneNumber = "0960000008", Email = "brandnew@test.com", Password = "pw123456", FullName = "Brand New" };

            var result = await _authService.RegisterAsync(request);

            var user = await _dbContext.Users.FindAsync(result.UserId);
            var profile = await _dbContext.CustomerProfiles.FirstOrDefaultAsync(p => p.UserId == result.UserId);
            var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == result.UserId);

            Assert.NotNull(user);
            Assert.NotNull(profile);
            Assert.NotNull(wallet);
            Assert.Equal(0, wallet.Balance);
        }

        [Fact]
        public async Task RegisterAsync_EmailSendFails_ShouldThrowBadRequestException()
        {
            _emailServiceMock
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP unreachable"));

            var request = new RegisterDTO { PhoneNumber = "0960000009", Email = "emailfail@test.com", Password = "pw123456", FullName = "Fail Guy" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
            Assert.Contains("OTP email could not be sent", ex.Message);
        }

        [Fact]
        public async Task RegisterAsync_ValidNewUser_ShouldReturnPendingResponse()
        {
            var request = new RegisterDTO { PhoneNumber = "0960000010", Email = "success@test.com", Password = "pw123456", FullName = "Success Guy" };

            var result = await _authService.RegisterAsync(request);

            Assert.Equal("success@test.com", result.Email);
            Assert.Equal(UserStatuses.Pending, result.Status);
            Assert.True(result.OtpExpiresAt > DateTime.UtcNow);
        }
    }
}
