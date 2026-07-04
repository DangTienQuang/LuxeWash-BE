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
    public class ServicePreferenceCalculator : IServicePreferenceCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            var completedBookings = snapshot.Bookings
                .Where(x => x.Status == "Completed")
                .ToList();

            if (!completedBookings.Any())
                return Task.CompletedTask;

            var bookingDetails = completedBookings
                .SelectMany(x => x.BookingDetails)
                .ToList();

            if (!bookingDetails.Any())
                return Task.CompletedTask;

            CalculateFavoriteService(profile, bookingDetails);

            CalculateServiceStatistics(profile, completedBookings, bookingDetails);

            return Task.CompletedTask;
        }

        private void CalculateFavoriteService(CustomerFeatureProfile profile, List<BookingDetail> bookingDetails)
        {
            var favorite = bookingDetails
                .GroupBy(x => x.ServiceId)
                .OrderByDescending(x => x.Count())
                .First();

            profile.FavoriteServiceId = favorite.Key;
            profile.FavoriteServiceUsage = favorite.Count();
        }

        private void CalculateServiceStatistics(CustomerFeatureProfile profile, List<Booking> bookings, List<BookingDetail> bookingDetails)
        {
            profile.TotalServicesPurchased = bookingDetails.Count;

            profile.AverageServicesPerBooking =
                bookings.Count == 0
                ? 0
                : (double)bookingDetails.Count / bookings.Count;
        }
    }
}
