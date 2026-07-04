using AutoWashPro.DAL.Entities;
using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using DAL.Entities;

namespace BLL.Services.AI.Calculators
{
    public class VisitFeatureCalculator : IVisitFeatureCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            var bookings = snapshot.Bookings
                .Where(x => x.Status == "Completed")
                .OrderBy(x => x.ScheduledTime)
                .ToList();

            profile.VisitCount = snapshot.Bookings.Count;

            profile.CompletedVisitCount = bookings.Count;

            profile.CancelledVisitCount =
                snapshot.Bookings.Count(x => x.Status == "Cancelled");

            profile.NoShowCount =
                snapshot.Bookings.Count(x => x.Status == "NoShow");

            CalculateDaysSinceLastVisit(profile, bookings);

            CalculateVisitGap(profile, bookings);

            CalculateVisitTrend(profile, bookings);

            CalculateFavoriteVisitDay(profile, bookings);

            CalculateFavoriteVisitHour(profile, bookings);

            CalculateVisitRates(profile, bookings);

            CalculateExpectedVisit(profile, bookings);

            return Task.CompletedTask;
        }

        private void CalculateDaysSinceLastVisit(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
            {
                profile.DaysSinceLastVisit = 9999;
                return;
            }

            profile.DaysSinceLastVisit =
                (DateTime.UtcNow - bookings.Last().ScheduledTime).Days;
        }

        private void CalculateVisitGap(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (bookings.Count < 2)
            {
                profile.AverageVisitGap = 0;
                profile.LongestVisitGap = 0;
                profile.ShortestVisitGap = 0;
                return;
            }

            var gaps = new List<int>();

            for (int i = 1; i < bookings.Count; i++)
            {
                gaps.Add(
                    (bookings[i].ScheduledTime -
                     bookings[i - 1].ScheduledTime).Days);
            }

            profile.AverageVisitGap = gaps.Average();

            profile.LongestVisitGap = gaps.Max();

            profile.ShortestVisitGap = gaps.Min();
        }

        private void CalculateVisitTrend(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (bookings.Count < 6)
            {
                profile.VisitTrend = "Insufficient Data";
                return;
            }

            var recent = bookings.TakeLast(3).ToList();

            var previous = bookings.Skip(bookings.Count - 6).Take(3).ToList();

            var recentGap =
                CalculateAverageGap(recent);

            var previousGap =
                CalculateAverageGap(previous);

            if (recentGap < previousGap * 0.9)
            {
                profile.VisitTrend = "Increasing";
            }
            else if (recentGap > previousGap * 1.1)
            {
                profile.VisitTrend = "Decreasing";
            }
            else
            {
                profile.VisitTrend = "Stable";
            }
        }

        private double CalculateAverageGap(List<Booking> bookings)
        {
            if (bookings.Count < 2)
                return 0;

            var gaps = new List<int>();

            for (int i = 1; i < bookings.Count; i++)
            {
                gaps.Add(
                    (bookings[i].ScheduledTime -
                     bookings[i - 1].ScheduledTime).Days);
            }

            return gaps.Average();
        }

        private void CalculateFavoriteVisitDay(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
                return;

            profile.FavoriteVisitDay = bookings
                    .GroupBy(x => x.ScheduledTime.DayOfWeek)
                    .OrderByDescending(x => x.Count())
                    .First()
                    .Key
                    .ToString();
        }

        private void CalculateFavoriteVisitHour(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
                return;

            profile.FavoriteVisitHour = bookings
                    .GroupBy(x => x.ScheduledTime.Hour)
                    .OrderByDescending(x => x.Count())
                    .First()
                    .Key;
        }

        private void CalculateVisitRates(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
                return;

            profile.WeekendVisitRate =
                bookings.Count(x =>
                        x.ScheduledTime.DayOfWeek == DayOfWeek.Saturday ||
                        x.ScheduledTime.DayOfWeek == DayOfWeek.Sunday)
                * 100.0 / bookings.Count;

            profile.MorningVisitRate =
                bookings.Count(x =>
                    x.ScheduledTime.Hour < 12)
                * 100.0 / bookings.Count;

            profile.AfternoonVisitRate =
                bookings.Count(x =>
                    x.ScheduledTime.Hour >= 12 &&
                    x.ScheduledTime.Hour < 18)
                * 100.0 / bookings.Count;

            profile.EveningVisitRate =
                bookings.Count(x =>
                    x.ScheduledTime.Hour >= 18)
                * 100.0 / bookings.Count;
        }

        private void CalculateExpectedVisit(CustomerFeatureProfile profile, List<Booking> bookings)
        {
            if (!bookings.Any())
                return;

            profile.ExpectedNextVisit = bookings.Last()
                    .ScheduledTime
                    .AddDays(profile.AverageVisitGap);
        }
    }
}