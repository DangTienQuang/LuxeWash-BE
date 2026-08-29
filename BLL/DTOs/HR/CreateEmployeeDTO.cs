using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class CreateEmployeeDTO
    {
        [Required]
        [MaxLength(20)]
        [RegularExpression(@"^0[35789][0-9]{8}$", ErrorMessage = "Phone number is invalid.")]
        public string PhoneNumber { get; set; } = null!;
        [Required]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Password must have at least 8 characters, including 1 uppercase letter and 1 digit.")]
        public string Password { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;
        [Required]
        [RegularExpression("^(Manager|Staff)$", ErrorMessage = "Role must be Manager or Staff.")]
        public string Role { get; set; } = null!;
        [Required(ErrorMessage = "Branch is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Branch is required.")]
        public int? BranchId { get; set; }
    }
}
