using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.BLL.Services
{
    public class OverloadSuggestionService : IOverloadSuggestionService
    {
        private readonly AutoWashDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;

        public OverloadSuggestionService(AutoWashDbContext context, IPushNotificationService pushNotificationService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
        }

        public async Task CheckAndTriggerOverloadAsync(int branchId)
        {
            var now = DateTime.UtcNow;
            var windowEnd = now.AddHours(2);
            var today = now.Date;
            var timeNow = now.TimeOfDay;
            var timeEnd = windowEnd.TimeOfDay;

            // 1. Check if overloaded
            var queueLength = await _context.Bookings
                .CountAsync(b => b.BranchId == branchId && b.Status == "CheckedIn" && b.ProcessingLaneId == null);

            var impactedBookings = await _context.Bookings
                .Include(b => b.User)
                .Where(b => b.BranchId == branchId 
                         && b.Status == "Pending" 
                         && !b.IsWaitAccepted
                         && b.UserId != null
                         && b.ScheduledTime >= now
                         && b.ScheduledTime <= windowEnd)
                .ToListAsync();

            // Total booked weight in the current window
            var totalBookedWeight = impactedBookings.Sum(b => b.CapacityWeight > 0 ? b.CapacityWeight : 1);

            // Get branch capacity for this window
            var relevantCapacities = await _context.DailySlotCapacities
                .Include(dsc => dsc.TimeSlot)
                .Where(dsc => dsc.BranchId == branchId && dsc.Date == today
                           && dsc.TimeSlot.StartTime < timeEnd 
                           && dsc.TimeSlot.EndTime > timeNow)
                .ToListAsync();

            var maxCapacity = relevantCapacities.Sum(c => c.TimeSlot.MaxCapacity);

            // Overload Condition: Walk-ins + Booked Cars >= MaxCapacity
            // Or if MaxCapacity is 0 (no slots config), fallback to QueueLength >= 5
            if (maxCapacity > 0)
            {
                if ((queueLength + totalBookedWeight) < maxCapacity) return; // Not overloaded
            }
            else
            {
                if (queueLength < 5) return; // Fallback threshold
            }

            if (!impactedBookings.Any()) return;

            var currentBranch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
            if (currentBranch == null || !currentBranch.Latitude.HasValue || !currentBranch.Longitude.HasValue) return;

            // 3. Find nearby branches
            var otherBranches = await _context.Branches
                .Where(b => b.IsActive && b.BranchId != branchId && b.Latitude.HasValue && b.Longitude.HasValue)
                .ToListAsync();

            var nearbyBranches = otherBranches
                .Select(b => new
                {
                    Branch = b,
                    Distance = CalculateHaversine(currentBranch.Latitude.Value, currentBranch.Longitude.Value, b.Latitude.Value, b.Longitude.Value)
                })
                .OrderBy(x => x.Distance)
                .Take(5) // Check top 5 closest
                .ToList();

            if (!nearbyBranches.Any()) return;

            // 4. For each booking, find a suitable branch
            foreach (var booking in impactedBookings)
            {
                var targetDate = booking.ScheduledTime.Date;
                var targetTime = booking.ScheduledTime.TimeOfDay;

                Branch? bestBranch = null;
                int bestSlotId = 0;

                foreach (var nb in nearbyBranches)
                {
                    // Check if target branch has walk-in jam
                    var targetQueueLength = await _context.Bookings
                        .CountAsync(b => b.BranchId == nb.Branch.BranchId && b.Status == "CheckedIn" && b.ProcessingLaneId == null);
                    
                    if (targetQueueLength >= 3) continue; // Too many walk-ins, don't suggest

                    // Check slot capacity
                    var dsc = await _context.DailySlotCapacities
                        .Include(c => c.TimeSlot)
                        .Where(c => c.BranchId == nb.Branch.BranchId 
                                 && c.Date == targetDate 
                                 && c.TimeSlot.StartTime <= targetTime 
                                 && c.TimeSlot.EndTime > targetTime)
                        .FirstOrDefaultAsync();

                    if (dsc != null && (dsc.BookedWeight + (booking.CapacityWeight > 0 ? booking.CapacityWeight : 1)) <= dsc.TimeSlot.MaxCapacity)
                    {
                        bestBranch = nb.Branch;
                        bestSlotId = dsc.SlotId;
                        break;
                    }
                }

                if (bestBranch != null)
                {
                    // Invalidate old suggestions for this booking
                    var oldSuggestions = await _context.OverloadSuggestions
                        .Where(s => s.BookingId == booking.BookingId && !s.IsProcessed)
                        .ToListAsync();
                    
                    foreach(var old in oldSuggestions)
                    {
                        old.IsProcessed = true;
                    }

                    // Save the suggestion to DB
                    var suggestion = new OverloadSuggestion
                    {
                        BookingId = booking.BookingId,
                        SuggestedBranchId = bestBranch.BranchId,
                        SuggestedBranchName = bestBranch.Name,
                        SuggestedSlotId = bestSlotId,
                        SuggestedTime = booking.ScheduledTime,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                    };
                    _context.OverloadSuggestions.Add(suggestion);
                    await _context.SaveChangesAsync(); // Save to generate ID

                    var pushRequest = new PushNotificationRequest
                    {
                        UserId = booking.UserId.Value,
                        Title = "Chi nhánh quá tải!",
                        Body = $"Chi nhánh {currentBranch.Name} hiện đang quá tải. Bạn có muốn đổi sang {bestBranch.Name} hoặc hủy/giữ chỗ? Nhận Voucher 10% nếu bạn đồng ý chuyển!",
                        Data = new OverloadNotificationData
                        {
                            BookingId = booking.BookingId,
                            SuggestedBranchId = bestBranch.BranchId,
                            SuggestedBranchName = bestBranch.Name,
                            SuggestedSlotId = bestSlotId,
                            SuggestedTime = booking.ScheduledTime
                        }
                    };

                    await _pushNotificationService.SendPushNotificationAsync(pushRequest);
                    booking.OverloadNotifiedAt = DateTime.UtcNow; 
                }
            }

            await _context.SaveChangesAsync();
        }

        private double CalculateHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of earth in km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = 
                Math.Sin(dLat/2) * Math.Sin(dLat/2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * 
                Math.Sin(dLon/2) * Math.Sin(dLon/2); 
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a)); 
            return R * c; 
        }
    }
}
