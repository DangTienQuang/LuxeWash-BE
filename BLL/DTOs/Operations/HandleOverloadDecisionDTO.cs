using System.ComponentModel.DataAnnotations;
namespace AutoWashPro.BLL.DTOs
{
    public class HandleOverloadDecisionDTO
    {
        [Required]
        public string Decision { get; set; } = null!;
    }
}
