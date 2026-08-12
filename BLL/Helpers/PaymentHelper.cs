using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace BLL.Helpers
{
    public static class PaymentHelper
    {
        public static async Task<bool> IsBookingPaidAsync(AutoWashDbContext context, Booking booking)
        {
            if (booking.FinalAmount == 0) return true;

            // Business bookings are settled through the approved company's
            // monthly credit account, not through an individual booking
            // transaction at the entrance gate.
            if (booking.BusinessProfileId.HasValue ||
                string.Equals(booking.BookingType, "Business", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(booking.BookingType, "Fleet", System.StringComparison.OrdinalIgnoreCase))
            {
                var businessProfileId = booking.BusinessProfileId;
                if (!businessProfileId.HasValue && booking.FleetVehicleId.HasValue)
                {
                    businessProfileId = await context.FleetVehicles
                        .Where(v => v.FleetVehicleId == booking.FleetVehicleId.Value)
                        .Select(v => (int?)v.BusinessProfileId)
                        .FirstOrDefaultAsync();
                }

                if (!businessProfileId.HasValue) return false;

                return await context.BusinessProfiles.AnyAsync(profile =>
                    profile.BusinessProfileId == businessProfileId.Value &&
                    profile.ApprovalStatus == "Approved");
            }

            var isPaid = await context.Transactions.AnyAsync(t => 
                t.ReferenceBookingId == booking.BookingId 
                && t.Status == "Completed"
                && (t.TransactionType == "Payment" || t.TransactionType == "WalkInPayment" || t.TransactionType == "BookingPayment"));

            return isPaid;
        }
    }
}
