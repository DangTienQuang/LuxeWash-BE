using System;

namespace AutoWashPro.BLL.DTOs
{
    public class OverloadSuggestionResponseDTO
    {
        public int SuggestionId { get; set; }
        public int BookingId { get; set; }
        public int SuggestedBranchId { get; set; }
        public string SuggestedBranchName { get; set; } = null!;
        public int SuggestedSlotId { get; set; }
        public DateTime SuggestedTime { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
