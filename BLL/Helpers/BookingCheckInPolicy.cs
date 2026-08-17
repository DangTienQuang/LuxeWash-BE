using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace BLL.Helpers
{
    public static class BookingCheckInPolicy
    {
        public const string WrongDateErrorCode = "BOOKING_CHECKIN_WRONG_DATE";
        public const string OutsideTimeErrorCode = "BOOKING_CHECKIN_OUTSIDE_TIME";

        public static async Task ValidateAsync(
            AutoWashDbContext context,
            Booking booking,
            bool allowOutsideScheduledTime = false)
        {
            var nowVn = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            // ScheduledTime is stored as the branch's local wall-clock time.
            // Do not add the UTC+7 offset again or the displayed booking date/time
            // and the validation window will differ by seven hours.
            var scheduledVn = booking.ScheduledTime;

            if (scheduledVn.Date != nowVn.Date)
            {
                throw new BadRequestException(
                    $"Không thể check-in xe {booking.LicensePlate}. Lịch đặt thuộc ngày {scheduledVn:dd/MM/yyyy}, hôm nay là {nowVn:dd/MM/yyyy}.",
                    WrongDateErrorCode);
            }

            var scheduledTime = scheduledVn.TimeOfDay;
            var slot = await context.TimeSlots
                .AsNoTracking()
                .Where(item =>
                    item.BranchId == booking.BranchId &&
                    item.StartTime <= scheduledTime &&
                    item.EndTime > scheduledTime)
                .OrderBy(item => item.StartTime)
                .FirstOrDefaultAsync();

            var windowStart = slot?.StartTime ?? scheduledTime;
            var windowEnd = slot?.EndTime ?? scheduledTime.Add(TimeSpan.FromMinutes(15));
            var isInsideScheduledWindow =
                nowVn.TimeOfDay >= windowStart && nowVn.TimeOfDay < windowEnd;

            if (!isInsideScheduledWindow && !allowOutsideScheduledTime)
            {
                throw new BadRequestException(
                    $"Xe {booking.LicensePlate} đến không đúng giờ đặt. Khung giờ đã đặt là {windowStart:hh\\:mm}–{windowEnd:hh\\:mm} ngày {scheduledVn:dd/MM/yyyy}; thời gian hiện tại là {nowVn:HH:mm}. Staff có muốn cho phép check-in ngoài giờ không?",
                    OutsideTimeErrorCode);
            }
        }
    }
}
