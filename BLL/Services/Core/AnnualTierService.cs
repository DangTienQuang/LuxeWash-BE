using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class AnnualTierService : IAnnualTierService
    {
        private readonly AutoWashDbContext _context;

        public AnnualTierService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task ResetAnnualTiersAsync(CancellationToken cancellationToken = default)
        {
            var allProfiles = await _context.CustomerProfiles.ToListAsync(cancellationToken);
            var allTiers = await _context.Tiers.OrderByDescending(t => t.MinAccumulatedPoints).ToListAsync(cancellationToken);

            foreach (var profile in allProfiles)
            {
                var newTier = allTiers.FirstOrDefault(t => profile.CurrentYearTierPoints >= t.MinAccumulatedPoints);
                if (newTier != null)
                {
                    profile.TierId = newTier.TierId;
                }

                profile.CurrentYearTierPoints = 0;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
