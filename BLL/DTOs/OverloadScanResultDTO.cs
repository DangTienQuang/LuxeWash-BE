namespace AutoWashPro.BLL.DTOs
{
    /// <summary>
    /// Result returned by the overload scan-and-notify operation.
    /// </summary>
    public class OverloadScanResultDTO
    {
        public int ScannedBookings { get; set; }
        public int CreatedSuggestions { get; set; }
        public int SkippedActiveSuggestions { get; set; }
        public int NotificationsSent { get; set; }
        public int NotificationsFailed { get; set; }
    }
}
