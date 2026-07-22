using System;

namespace AutoWashPro.BLL.DTOs
{
    public class HandleOverloadDecisionDTO
    {
        // "Switch", "Cancel", "Keep"
        public string Decision { get; set; } = null!;
        public int? SuggestedBranchId { get; set; }
        public int? SuggestedSlotId { get; set; }
        public DateTime? SuggestedTime { get; set; }
    }
}
