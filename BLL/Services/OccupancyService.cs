using System;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class OccupancyService : IOccupancyService
    {
        private readonly AutoWashDbContext _context;

        public OccupancyService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<double> GetBranchOccupancyRateAsync(int branchId, DateTime targetDate)
        {
            var targetDateTime = targetDate;
            var endDateTime = targetDateTime.AddHours(4);

            var startDate = targetDateTime.Date;
            var endDate = endDateTime.Date;

            var dailyCapacities = await _context.DailySlotCapacities
                .Include(dsc => dsc.TimeSlot)
                .Where(dsc => dsc.BranchId == branchId && dsc.Date >= startDate && dsc.Date <= endDate)
                .ToListAsync();

            // Safely compute the slot start date/time and filter within the next 4 hours
            var relevantCapacities = dailyCapacities
                .Where(dsc => dsc.TimeSlot != null &&
                              dsc.Date.Add(dsc.TimeSlot.StartTime) >= targetDateTime &&
                              dsc.Date.Add(dsc.TimeSlot.StartTime) < endDateTime)
                .ToList();

            if (!relevantCapacities.Any())
            {
                return 0.0;
            }

            int totalBookedWeight = relevantCapacities.Sum(dsc => dsc.BookedWeight);
            int totalMaxCapacity = relevantCapacities.Sum(dsc => dsc.TimeSlot?.MaxCapacity ?? 0);

            if (totalMaxCapacity <= 0)
            {
                return 0.0;
            }

            return (double)totalBookedWeight / totalMaxCapacity;
        }
    }
}
