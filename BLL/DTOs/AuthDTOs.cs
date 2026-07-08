using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "Số điện thoại không được để trống.")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự, gồm 1 chữ hoa và 1 chữ số.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Họ tên không được chỉ chứa khoảng trắng.")]
        public string FullName { get; set; }
    }
    public class ResendOtpDTO
    {
        public string Email { get; set; } = null!;
    }
    public class LoginDTO
    {
        [Required(ErrorMessage = "Số điện thoại hoặc Email không được để trống.")]
        public string PhoneOrEmail { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
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
        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mã OTP không được để trống.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
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
        [Required(ErrorMessage = "Access Token không được để trống.")]
        public string AccessToken { get; set; }

        [Required(ErrorMessage = "Refresh Token không được để trống.")]
        public string RefreshToken { get; set; }
    }

    public class ChangePasswordDTO
    {
        [Required(ErrorMessage = "Mật khẩu cũ không được để trống.")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm 1 chữ hoa và 1 chữ số.")]
        public string NewPassword { get; set; }
    }

    public class ForgotPasswordDTO
    {
        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; }
    }

    public class ResetPasswordDTO
    {
        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mã OTP không được để trống.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải gồm 6 chữ số.")]
        public string Otp { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự, gồm 1 chữ hoa và 1 chữ số.")]
        public string NewPassword { get; set; }
    }
}
