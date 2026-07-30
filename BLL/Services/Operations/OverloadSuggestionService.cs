using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.DAL.Entities;
using Microsoft.Extensions.Logging;

namespace AutoWashPro.BLL.Services
{
    public class OverloadSuggestionService : IOverloadSuggestionService
    {
        private readonly AutoWashDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<OverloadSuggestionService> _logger;

        public OverloadSuggestionService(
            AutoWashDbContext context,
            IPushNotificationService pushNotificationService,
            ILogger<OverloadSuggestionService> logger)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        /// <summary>
        /// P0.3 + P0.5: Checks branch overload, creates OverloadSuggestion rows atomically per booking,
        /// and sends FCM. Returns a structured scan result.
        /// </summary>
        public async Task<OverloadScanResultDTO> CheckAndTriggerOverloadAsync(int branchId)
        {
            var result = new OverloadScanResultDTO();

            var now = DateTime.UtcNow;
            var windowEnd = now.AddHours(2);
            var today = now.Date;
            var timeNow = now.TimeOfDay;
            var timeEnd = windowEnd.TimeOfDay;

            // 1. Check if branch is overloaded
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

            result.ScannedBookings = impactedBookings.Count;

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

            // Not overloaded — early exit
            if (maxCapacity > 0)
            {
                if ((queueLength + totalBookedWeight) < maxCapacity) return result;
            }
            else
            {
                if (queueLength < 5) return result; // Fallback threshold
            }

            if (!impactedBookings.Any()) return result;

            var currentBranch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
            if (currentBranch == null || !currentBranch.Latitude.HasValue || !currentBranch.Longitude.HasValue)
                return result;

            // 2. Find nearby active branches
            var otherBranches = await _context.Branches
                .Where(b => b.IsActive && b.BranchId != branchId && b.Latitude.HasValue && b.Longitude.HasValue)
                .ToListAsync();

            var nearbyBranches = otherBranches
                .Select(b => new
                {
                    Branch = b,
                    Distance = CalculateHaversine(
                        currentBranch.Latitude!.Value, currentBranch.Longitude!.Value,
                        b.Latitude!.Value, b.Longitude!.Value)
                })
                .OrderBy(x => x.Distance)
                .Take(5)
                .ToList();

            if (!nearbyBranches.Any()) return result;

            // 3. For each impacted booking, atomically check + create suggestion
            foreach (var booking in impactedBookings)
            {
                var targetDate = booking.ScheduledTime.Date;
                var targetTime = booking.ScheduledTime.TimeOfDay;

                // P0.5: Atomic check-and-create using a Serializable transaction per booking
                // to prevent two concurrent triggers from inserting duplicate active suggestions.
                using var suggTx = await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);
                try
                {
                    // Re-check inside the transaction
                    var activeSuggestion = await _context.OverloadSuggestions
                        .Where(s => s.BookingId == booking.BookingId && !s.IsProcessed && s.ExpiresAt > DateTime.UtcNow)
                        .FirstOrDefaultAsync();

                    if (activeSuggestion != null)
                    {
                        // Already has an active suggestion — skip
                        result.SkippedActiveSuggestions++;
                        await suggTx.RollbackAsync();
                        continue;
                    }

                    // Find the best available destination branch/slot
                    Branch? bestBranch = null;
                    int bestSlotId = 0;

                    foreach (var nb in nearbyBranches)
                    {
                        var targetQueueLength = await _context.Bookings
                            .CountAsync(b => b.BranchId == nb.Branch.BranchId
                                         && b.Status == "CheckedIn"
                                         && b.ProcessingLaneId == null);

                        if (targetQueueLength >= 3) continue; // Too busy

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

                    if (bestBranch == null)
                    {
                        await suggTx.RollbackAsync();
                        continue; // No suitable destination found
                    }

                    // Invalidate any stale (expired but not yet marked processed) suggestions
                    var stale = await _context.OverloadSuggestions
                        .Where(s => s.BookingId == booking.BookingId && !s.IsProcessed)
                        .ToListAsync();
                    foreach (var s in stale) s.IsProcessed = true;

                    // Insert new suggestion
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
                    await _context.SaveChangesAsync(); // Flush to generate suggestion.Id

                    booking.OverloadNotifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    await suggTx.CommitAsync();

                    result.CreatedSuggestions++;

                    // 4. Send FCM after commit so no notification on failed transaction
                    var pushRequest = new PushNotificationRequest
                    {
                        UserId = booking.UserId!.Value,
                        Title = "Branch Overloaded",
                        Body = $"Branch '{currentBranch.Name}' is currently overloaded. " +
                               $"Switch to '{bestBranch.Name}' and receive a 10% compensation voucher!",
                        Data = new OverloadNotificationData
                        {
                            SuggestionId = suggestion.Id,
                            BookingId = booking.BookingId,
                            SuggestedBranchId = bestBranch.BranchId,
                            SuggestedBranchName = bestBranch.Name,
                            SuggestedSlotId = bestSlotId,
                            SuggestedTime = booking.ScheduledTime,
                            ExpiresAt = suggestion.ExpiresAt
                        }
                    };

                    try
                    {
                        await _pushNotificationService.SendPushNotificationAsync(pushRequest);
                        result.NotificationsSent++;
                    }
                    catch (Exception ex)
                    {
                        // FCM failure must NOT undo the already-committed suggestion
                        _logger.LogError(ex,
                            "Failed to send overload FCM for Booking {BookingId} (SuggestionId={SuggestionId}). " +
                            "Suggestion is committed; customer may not be notified in real time.",
                            booking.BookingId, suggestion.Id);
                        result.NotificationsFailed++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error creating overload suggestion for Booking {BookingId}. Rolling back.",
                        booking.BookingId);
                    try { await suggTx.RollbackAsync(); } catch { /* already rolled back */ }
                }
            }

            return result;
        }

        private static double CalculateHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth radius in km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}
