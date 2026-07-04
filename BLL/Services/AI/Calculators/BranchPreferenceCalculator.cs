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
    public class BranchPreferenceCalculator : IBranchPreferenceCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            var completedBookings = snapshot.Bookings
                .Where(x => x.Status == "Completed")
                .ToList();

            if (!completedBookings.Any())
                return Task.CompletedTask;

            CalculateFavoriteBranch(profile, completedBookings);

            return Task.CompletedTask;
        }

        private void CalculateFavoriteBranch(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            var favorite = bookings
                .GroupBy(x => x.BranchId)
                .OrderByDescending(x => x.Count())
                .First();

            profile.FavoriteBranchId = favorite.Key;
            profile.FavoriteBranchVisits = favorite.Count();

            profile.BranchLoyaltyRate =
                (double)favorite.Count() / bookings.Count * 100;
        }
    }
}
