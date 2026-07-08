using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Phone number is invalid.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Password must have at least 8 characters, including 1 uppercase letter and 1 digit.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Full name cannot consist of only whitespace.")]
        public string FullName { get; set; }
    }
    public class ResendOtpDTO
    {
        public string Email { get; set; } = null!;
    }
    public class LoginDTO
    {
        [Required(ErrorMessage = "Phone number or email is required.")]
        public string PhoneOrEmail { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }
    }

    public class RegisterPendingResponseDTO
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
        public DateTime OtpExpiresAt { get; set; }
    }

    public class VerifyOtpDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "OTP code is required.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must be 6 digits.")]
        public string Otp { get; set; }
    }

    public class AuthResponseDTO
    {
        public int UserId { get; set; }
        public string PhoneNumber { get; set; }
        public string FullName { get; set; }
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public string Role { get; set; }
    }
    public class RefreshTokenDTO
    {
        [Required(ErrorMessage = "Access Token is required.")]
        public string AccessToken { get; set; }

        [Required(ErrorMessage = "Refresh Token is required.")]
        public string RefreshToken { get; set; }
    }

    public class ChangePasswordDTO
    {
        [Required(ErrorMessage = "Old password is required.")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "New password must have at least 8 characters, including 1 uppercase letter and 1 digit.")]
        public string NewPassword { get; set; }
    }

    public class ForgotPasswordDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; }
    }

    public class ResetPasswordDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "OTP code is required.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must be 6 digits.")]
        public string Otp { get; set; }

        [Required(ErrorMessage = "New password is required.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "New password must have at least 8 characters, including 1 uppercase letter and 1 digit.")]
        public string NewPassword { get; set; }
    }
}
