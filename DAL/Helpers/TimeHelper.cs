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
        public static DateTime ConvertToVnTime(DateTime dt)
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

            if (dt.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(dt, vnTimeZone);
            }
            else if (dt.Kind == DateTimeKind.Local)
            {
                return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.Local, vnTimeZone);
            }
            else
            {
                // Unspecified, assume it's already VN time or just treat as is
                return dt;
            }
        }

        public static DateTime? ConvertToVnTime(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            return ConvertToVnTime(dt.Value);
        }
    }
}
