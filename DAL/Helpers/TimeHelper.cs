using System;

namespace AutoWashPro.DAL.Helpers
{
    public static class TimeHelper
    {
        public static DateTime VnNow
        {
            get
            {
                TimeZoneInfo vnTimeZone;
                try
                {
                    vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            }
        }
    }
}
