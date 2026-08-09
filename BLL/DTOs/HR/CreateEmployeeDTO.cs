using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class CreateEmployeeDTO
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;
        [Required]
        [RegularExpression("^(Manager|Staff)$", ErrorMessage = "Role must be Manager or Staff.")]
        public string Role { get; set; } = null!;
        public int? BranchId { get; set; }
    }
}
