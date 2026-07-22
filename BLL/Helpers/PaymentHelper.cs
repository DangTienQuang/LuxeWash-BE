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

            var isPaid = await context.Transactions.AnyAsync(t => 
                t.ReferenceBookingId == booking.BookingId 
                && t.Status == "Completed" 
                && (t.TransactionType == "Payment" || t.TransactionType == "WalkInPayment" || t.TransactionType == "BookingPayment"));

            return isPaid;
        }
    }
}
