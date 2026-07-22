using System;

namespace AutoWashPro.BLL.DTOs
{
    public class PushNotificationRequest
    {
        public int UserId { get; set; }
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public object? Data { get; set; }
    }

    public class OverloadNotificationData
    {
        public string Type { get; set; } = "OVERLOAD_SUGGESTION";
        public int SuggestionId { get; set; }
        public int BookingId { get; set; }
        public int SuggestedBranchId { get; set; }
        public string SuggestedBranchName { get; set; } = null!;
        public int SuggestedSlotId { get; set; }
        public DateTime SuggestedTime { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
