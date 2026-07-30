using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class UpdateLaneDTO
    {
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = null!;

        [Required]
        public int BranchId { get; set; }

        public bool IsActive { get; set; }
        public bool IsBusinessLane { get; set; }
        public bool IsVipLane { get; set; }
    }
}
