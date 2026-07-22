using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class HandleOverloadDecisionDTO
    {
        /// <summary>
        /// Valid values: "Switch", "Cancel", "Keep"
        /// </summary>
        [Required]
        public string Decision { get; set; } = null!;
    }
}
