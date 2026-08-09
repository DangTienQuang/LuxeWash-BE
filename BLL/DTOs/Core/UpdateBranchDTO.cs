using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class UpdateBranchDTO
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        [MaxLength(255)]
        public string? Address { get; set; }
        public bool IsActive { get; set; }
    }
}
