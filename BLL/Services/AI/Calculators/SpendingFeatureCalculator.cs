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
    public class SpendingFeatureCalculator : ISpendingFeatureCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            var completedBookings = snapshot.Bookings
                .Where(x => x.Status == "Completed")
                .ToList();

            CalculateLifetimeSpend(profile, completedBookings);

            CalculateAverageSpend(profile, completedBookings);

            CalculateHighestSpend(profile, completedBookings);

            CalculateLowestSpend(profile, completedBookings);

            CalculateLifetimeBookings(profile, completedBookings);

            return Task.CompletedTask;
        }

        private void CalculateLifetimeSpend(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            profile.LifetimeSpend = bookings.Sum(x => x.FinalAmount);
        }

        private void CalculateAverageSpend(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
            {
                profile.AverageSpend = 0;
                return;
            }

            profile.AverageSpend = bookings.Average(x => x.FinalAmount);
        }

        private void CalculateHighestSpend(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
            {
                profile.HighestSpend = 0;
                return;
            }

            profile.HighestSpend = bookings.Max(x => x.FinalAmount);
        }

        private void CalculateLowestSpend(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
            {
                profile.LowestSpend = 0;
                return;
            }

            profile.LowestSpend = bookings.Min(x => x.FinalAmount);
        }

        private void CalculateLifetimeBookings(
            CustomerFeatureProfile profile,
            List<Booking> bookings)
        {
            profile.LifetimeBookings = bookings.Count;
        }
    }
}
