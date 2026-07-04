using AutoWashPro.DAL.Entities;
using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Calculators
{
    public class PromotionFeatureCalculator : IPromotionFeatureCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            var bookings = snapshot.Bookings
                .Where(x => x.Status == "Completed")
                .ToList();

            if (!bookings.Any())
                return Task.CompletedTask;

            CalculateCouponUsage(profile, bookings);

            CalculatePointUsage(profile, bookings);

            CalculateDiscountStatistics(profile, bookings);

            return Task.CompletedTask;
        }

        private void CalculateCouponUsage(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            profile.VoucherBookings =
                bookings.Count(x => x.AppliedVoucherId != null);

            profile.CouponUsageRate =
                profile.VoucherBookings * 100.0 /
                bookings.Count;
        }

        private void CalculatePointUsage(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            profile.PointBookings =
                bookings.Count(x => x.PointsUsed > 0);

            profile.PointUsageRate =
                profile.PointBookings * 100.0 /
                bookings.Count;
        }

        private void CalculateDiscountStatistics(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            profile.TotalVoucherSavings =
                bookings.Sum(x => x.VoucherDiscountAmount);

            profile.TotalPointSavings =
                bookings.Sum(x => x.PointDiscountAmount);

            profile.AverageVoucherDiscount =
                profile.VoucherBookings == 0
                ? 0
                : bookings
                    .Where(x => x.VoucherDiscountAmount > 0)
                    .Average(x => x.VoucherDiscountAmount);

            profile.AveragePointDiscount =
                profile.PointBookings == 0
                ? 0
                : bookings
                    .Where(x => x.PointDiscountAmount > 0)
                    .Average(x => x.PointDiscountAmount);

            profile.AverageDiscountReceived =
                bookings.Average(x =>
                    x.PointDiscountAmount +
                    x.VoucherDiscountAmount);
        }

        
    }
}
