using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class CreateLaneDTO
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;
        [Required]
        public int BranchId { get; set; }
        public bool IsBusinessLane { get; set; }
        public bool IsVipLane { get; set; }
    }
    public class CreateBusinessLaneDTO
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
        [Required]
        public int BranchId { get; set; }
    }
}
