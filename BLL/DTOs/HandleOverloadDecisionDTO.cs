using System;

namespace AutoWashPro.BLL.DTOs
{
    public class HandleOverloadDecisionDTO
    {
        public int SuggestionId { get; set; }
        // "Switch", "Cancel", "Keep"
        public string Decision { get; set; } = null!;
    }
}
