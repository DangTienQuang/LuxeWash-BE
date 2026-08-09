using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class TransferEmployeeDTO
    {
        [Required]
        public int BranchId { get; set; }
    }
}
