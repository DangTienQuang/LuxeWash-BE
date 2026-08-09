namespace AutoWashPro.BLL.DTOs
{
    public class OverloadScanResultDTO
    {
        public int ScannedBookings { get; set; }
        public int CreatedSuggestions { get; set; }
        public int SkippedActiveSuggestions { get; set; }
        public int NotificationsSent { get; set; }
        public int NotificationsFailed { get; set; }
    }
}
