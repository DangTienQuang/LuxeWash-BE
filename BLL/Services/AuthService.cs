using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class AuthService : IAuthService
    {
        private readonly AutoWashDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthService(AutoWashDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }
        public async Task<RegisterPendingResponseDTO> RegisterAsync(RegisterDTO request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var normalizedEmail = request.Email.Trim().ToLower();
                var existingUser = await _context.Users
                    .Include(u => u.CustomerProfile)
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber || (u.Email != null && u.Email.ToLower() == normalizedEmail));

                if (existingUser != null && existingUser.Status != UserStatuses.Pending)
                {
                    if (existingUser.PhoneNumber == request.PhoneNumber) throw new BadRequestException("Số điện thoại này đã được đăng ký.");
                    throw new BadRequestException("Email này đã được đăng ký.");
                }

                var emailUsedByOtherUser = await _context.Users.AnyAsync(u =>
                    u.Email != null
                    && u.Email.ToLower() == normalizedEmail
                    && (existingUser == null || u.UserId != existingUser.UserId));
                if (emailUsedByOtherUser) throw new BadRequestException("Email này đã được đăng ký.");

                var defaultTier = await _context.Tiers.FirstOrDefaultAsync(t => t.MinAccumulatedPoints == 0);

                if (defaultTier == null)
                {
                    defaultTier = new Tier
                    {
                        TierName = "Standard",
                        PointMultiplier = 1.0,
                        BookingWindowDays = 7,
                        MinAccumulatedPoints = 0
                    };
                    _context.Tiers.Add(defaultTier);
                    await _context.SaveChangesAsync();
                }

                var otp = GenerateOtp();
                var otpHash = HashOtp(otp);
                var otpExpiresAt = DateTime.UtcNow.AddMinutes(10);

                User user;
                if (existingUser != null)
                {
                    if (existingUser.PhoneNumber != request.PhoneNumber && string.Equals(existingUser.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                        throw new BadRequestException("Email này đang chờ xác thực cho tài khoản khác.");

                    user = existingUser;
                    user.Email = normalizedEmail;
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    user.Role = UserRoles.Customer;
                    user.Status = UserStatuses.Pending;
                    user.EmailVerificationOtpHash = otpHash;
                    user.EmailVerificationOtpExpiresAt = otpExpiresAt;

                    if (user.CustomerProfile != null)
                    {
                        user.CustomerProfile.FullName = request.FullName;
                    }
                }
                else
                {
                    user = new User
                    {
                        PhoneNumber = request.PhoneNumber,
                        Email = normalizedEmail,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                        Role = UserRoles.Customer,
                        Status = UserStatuses.Pending,
                        EmailVerificationOtpHash = otpHash,
                        EmailVerificationOtpExpiresAt = otpExpiresAt
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    var profile = new CustomerProfile
                    {
                        UserId = user.UserId,
                        FullName = request.FullName,
                        TierId = defaultTier.TierId,
                        ChurnScore = 0,
                        TotalPoint = 0,
                        PromotionPoint = 0
                    };
                    _context.CustomerProfiles.Add(profile);

                    var wallet = new Wallet
                    {
                        UserId = user.UserId,
                        Balance = 0,
                        Status = "Active"
                    };
                    _context.Wallets.Add(wallet);
                }

                await _context.SaveChangesAsync();

                try
                {
                    await SendRegistrationOtpEmailAsync(normalizedEmail, request.FullName, otp, otpExpiresAt);
                }
                catch (Exception ex)
                {
                    throw new BadRequestException($"Đăng ký bị từ chối do không thể gửi email OTP. Lỗi máy chủ Email: {ex.Message}");
                }

                await transaction.CommitAsync();

                return new RegisterPendingResponseDTO
                {
                    UserId = user.UserId,
                    Email = normalizedEmail,
                    Status = user.Status, 
                    OtpExpiresAt = otpExpiresAt
                };
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                throw new BadRequestException("Số điện thoại hoặc email này đã được đăng ký.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<AuthResponseDTO> LoginAsync(LoginDTO request)
        {
            var loginInput = request.PhoneOrEmail.Trim().ToLower();
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile)
                .FirstOrDefaultAsync(u => u.PhoneNumber == loginInput || (u.Email != null && u.Email.ToLower() == loginInput));

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedException("Số điện thoại/Email hoặc mật khẩu không chính xác.");

            if (user.Status == UserStatuses.Pending)
                throw new UnauthorizedException("Tài khoản chưa xác thực email. Vui lòng nhập mã OTP đã gửi tới email.");

            if (user.Status != UserStatuses.Active)
                throw new UnauthorizedException("Tài khoản đã bị khóa hoặc không hoạt động.");

            var token = CreateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return new AuthResponseDTO
            {
                UserId = user.UserId,
                PhoneNumber = user.PhoneNumber,
                FullName = GetFullName(user),
                Role = user.Role,
                Token = token,
                RefreshToken = refreshToken
            };
        }

        public async Task<AuthResponseDTO> VerifyOtpAsync(VerifyOtpDTO request)
        {
            var normalizedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);

            if (user == null) throw new NotFoundException("Không tìm thấy tài khoản với email này.");
            if (user.Status != UserStatuses.Pending) throw new BadRequestException("Tài khoản này không ở trạng thái chờ xác thực.");
            if (string.IsNullOrWhiteSpace(user.EmailVerificationOtpHash) || user.EmailVerificationOtpExpiresAt == null)
                throw new BadRequestException("Tài khoản chưa có mã OTP xác thực. Vui lòng đăng ký lại.");
            if (user.EmailVerificationOtpExpiresAt <= DateTime.UtcNow)
                throw new BadRequestException("Mã OTP đã hết hạn. Vui lòng đăng ký lại để nhận mã mới.");
            if (!string.Equals(user.EmailVerificationOtpHash, HashOtp(request.Otp), StringComparison.Ordinal))
                throw new BadRequestException("Mã OTP không chính xác.");

            user.Status = UserStatuses.Active;
            user.EmailVerificationOtpHash = null;
            user.EmailVerificationOtpExpiresAt = null;

            var token = CreateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            var welcomeVouchers = await _context.Vouchers
                .Where(v => v.CampaignType == AutoWashPro.DAL.Enums.VoucherCampaignType.Welcome && v.IsActive &&
                            (!v.StartDate.HasValue || v.StartDate <= DateTime.UtcNow) &&
                            v.ExpiryDate > DateTime.UtcNow)
                .ToListAsync();

            foreach(var voucher in welcomeVouchers)
            {
                var expiry = voucher.ExpiryDays.HasValue ? DateTime.UtcNow.AddDays(voucher.ExpiryDays.Value) : voucher.ExpiryDate;
                _context.UserVouchers.Add(new UserVoucher
                {
                    UserId = user.UserId,
                    VoucherId = voucher.VoucherId,
                    IsUsed = false,
                    UsageCount = 0,
                    ReceivedDate = DateTime.UtcNow,
                    ExpiryDate = expiry <= voucher.ExpiryDate ? expiry : voucher.ExpiryDate
                });
            }

            await _context.SaveChangesAsync();

            return new AuthResponseDTO
            {
                UserId = user.UserId,
                PhoneNumber = user.PhoneNumber,
                FullName = user.CustomerProfile?.FullName,
                Role = user.Role,
                Token = token,
                RefreshToken = refreshToken
            };
        }

        public async Task<AuthResponseDTO> RefreshTokenAsync(RefreshTokenDTO request)
        {
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null) throw new UnauthorizedException("Access token không hợp lệ.");

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) throw new UnauthorizedException("Token không chứa thông tin User.");

            int userId = int.Parse(userIdClaim);
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new UnauthorizedException("Refresh token không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.");

            var newAccessToken = CreateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            await _context.SaveChangesAsync();

            return new AuthResponseDTO
            {
                UserId = user.UserId,
                PhoneNumber = user.PhoneNumber,
                FullName = GetFullName(user),
                Role = user.Role,
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDTO request)
        {
            if (request.OldPassword == request.NewPassword)
                throw new BadRequestException("Mật khẩu mới không được trùng với mật khẩu cũ.");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new NotFoundException("Không tìm thấy người dùng.");

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
                throw new BadRequestException("Mật khẩu cũ không chính xác.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return true;
        }

        private string CreateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var branchId = user.EmployeeProfile?.BranchId;
            if (branchId.HasValue)
            {
                claims.Add(new Claim("BranchId", branchId.Value.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static string GetFullName(User user)
        {
            return user.Role switch
            {
                UserRoles.Manager => user.ManagerProfile?.FullName ?? user.PhoneNumber,
                UserRoles.Staff => user.StaffProfile?.FullName ?? user.PhoneNumber,
                UserRoles.Customer => user.CustomerProfile?.FullName ?? user.PhoneNumber,
                _ => user.CustomerProfile?.FullName
                    ?? user.ManagerProfile?.FullName
                    ?? user.StaffProfile?.FullName
                    ?? user.PhoneNumber
            };
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        private string GenerateOtp()
        {
            return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        }

        private string HashOtp(string otp)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
            return Convert.ToHexString(bytes);
        }

        private Task SendRegistrationOtpEmailAsync(string email, string fullName, string otp, DateTime otpExpiresAt)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px;'>
                    <h2 style='color: #007bff; text-align: center;'>SMARTWASH XÁC THỰC EMAIL</h2>
                    <p>Xin chào <b>{fullName}</b>,</p>
                    <p>Mã OTP đăng ký tài khoản SmartWash của bạn là:</p>
                    <div style='font-size: 32px; font-weight: bold; letter-spacing: 6px; text-align: center; padding: 16px; background: #f3f7ff; border-radius: 8px;'>{otp}</div>
                    <p>Mã này có hiệu lực trong 10 phút, đến <b>{otpExpiresAt.ToLocalTime():dd/MM/yyyy HH:mm}</b>.</p>
                    <p>Nếu bạn không thực hiện đăng ký, vui lòng bỏ qua email này.</p>
                    <p>Trân trọng,<br><b>Đội ngũ SmartWash</b></p>
                </div>";

            return _emailService.SendEmailAsync(email, "[SmartWash] Mã OTP xác thực đăng ký", html);
        }
        public async Task<RegisterPendingResponseDTO> ResendOtpAsync(ResendOtpDTO request)
        {
            var normalizedEmail = request.Email.Trim().ToLower();

            // Tìm user dựa trên email
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);

            if (user == null)
                throw new NotFoundException("Không tìm thấy tài khoản nào đăng ký bằng email này.");

            if (user.Status != UserStatuses.Pending)
                throw new BadRequestException("Tài khoản đã được xác thực hoặc đang ở trạng thái không hợp lệ.");

            // Sinh OTP mới và set lại thời gian 10 phút
            var otp = GenerateOtp();
            var otpHash = HashOtp(otp);
            var otpExpiresAt = DateTime.UtcNow.AddMinutes(10);

            user.EmailVerificationOtpHash = otpHash;
            user.EmailVerificationOtpExpiresAt = otpExpiresAt;

            await _context.SaveChangesAsync();

            // Gửi email mới
            var fullName = user.CustomerProfile?.FullName ?? "Quý khách";
            try
            {
                await SendRegistrationOtpEmailAsync(normalizedEmail, fullName, otp, otpExpiresAt);
            }
            catch (Exception ex)
            {
                throw new BadRequestException($"Không thể gửi email OTP mới. Lỗi máy chủ Email: {ex.Message}");
            }

            // Trả về response y hệt như lúc Register để FE hiển thị lại bộ đếm ngược
            return new RegisterPendingResponseDTO
            {
                UserId = user.UserId,
                Email = normalizedEmail,
                Status = user.Status,
                OtpExpiresAt = otpExpiresAt
            };
        }
        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_configuration["Jwt:Key"])),
                ValidateLifetime = false 
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;

            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Token không hợp lệ.");

            return principal;
        }

        public async Task LogoutAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new NotFoundException("Không tìm thấy người dùng.");

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _context.SaveChangesAsync();
        }

        public async Task ForgotPasswordAsync(ForgotPasswordDTO request)
        {
            var normalizedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users
                .Include(u => u.CustomerProfile)
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);

            if (user == null)
                throw new NotFoundException("Không tìm thấy tài khoản nào với email này.");

            if (user.Status != UserStatuses.Active)
                throw new BadRequestException("Tài khoản chưa được kích hoạt hoặc đã bị khóa.");

            var otp = GenerateOtp();
            var otpHash = HashOtp(otp);
            var otpExpiresAt = DateTime.UtcNow.AddMinutes(10);

            user.EmailVerificationOtpHash = otpHash;
            user.EmailVerificationOtpExpiresAt = otpExpiresAt;
            await _context.SaveChangesAsync();

            var fullName = GetFullName(user);

            try
            {
                await SendForgotPasswordOtpEmailAsync(normalizedEmail, fullName, otp, otpExpiresAt);
            }
            catch (Exception ex)
            {
                throw new BadRequestException($"Không thể gửi email OTP đặt lại mật khẩu. Lỗi: {ex.Message}");
            }
        }

        public async Task ResetPasswordAsync(ResetPasswordDTO request)
        {
            var normalizedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);

            if (user == null)
                throw new NotFoundException("Không tìm thấy tài khoản nào với email này.");

            if (user.Status != UserStatuses.Active)
                throw new BadRequestException("Tài khoản chưa được kích hoạt hoặc đã bị khóa.");

            if (string.IsNullOrWhiteSpace(user.EmailVerificationOtpHash) || user.EmailVerificationOtpExpiresAt == null)
                throw new BadRequestException("Chưa có yêu cầu đặt lại mật khẩu. Vui lòng gửi yêu cầu quên mật khẩu trước.");

            if (user.EmailVerificationOtpExpiresAt <= DateTime.UtcNow)
                throw new BadRequestException("Mã OTP đã hết hạn. Vui lòng gửi lại yêu cầu quên mật khẩu.");

            if (!string.Equals(user.EmailVerificationOtpHash, HashOtp(request.Otp), StringComparison.Ordinal))
                throw new BadRequestException("Mã OTP không chính xác.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.EmailVerificationOtpHash = null;
            user.EmailVerificationOtpExpiresAt = null;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _context.SaveChangesAsync();
        }

        private Task SendForgotPasswordOtpEmailAsync(string email, string fullName, string otp, DateTime otpExpiresAt)
        {
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px;'>
                    <h2 style='color: #dc3545; text-align: center;'>SMARTWASH ĐẶT LẠI MẬT KHẨU</h2>
                    <p>Xin chào <b>{fullName}</b>,</p>
                    <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản SmartWash của bạn. Mã OTP xác thực:</p>
                    <div style='font-size: 32px; font-weight: bold; letter-spacing: 6px; text-align: center; padding: 16px; background: #fff3f3; border-radius: 8px; color: #dc3545;'>{otp}</div>
                    <p>Mã này có hiệu lực trong 10 phút, đến <b>{otpExpiresAt.ToLocalTime():dd/MM/yyyy HH:mm}</b>.</p>
                    <p style='color: #888;'>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email và đổi mật khẩu ngay lập tức nếu bạn nghi ngờ tài khoản bị xâm phạm.</p>
                    <p>Trân trọng,<br><b>Đội ngũ SmartWash</b></p>
                </div>";

            return _emailService.SendEmailAsync(email, "[SmartWash] Mã OTP đặt lại mật khẩu", html);
        }
    }
}
