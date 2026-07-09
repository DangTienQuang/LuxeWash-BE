using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.BLL.Exceptions;

namespace AutoWashPro.BLL.Services
{
    public class TierService : ITierService
    {
        private readonly AutoWashDbContext _context;

        public TierService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<TierResponseDTO>> GetTiersAsync()
        {
            return await _context.Tiers
                .OrderBy(t => t.MinAccumulatedPoints)
                .Select(t => new TierResponseDTO
                {
                    TierId = t.TierId,
                    TierName = t.TierName,
                    PointMultiplier = t.PointMultiplier,
                    BookingWindowDays = t.BookingWindowDays,
                    MinAccumulatedPoints = t.MinAccumulatedPoints
                }).ToListAsync();
        }

        public async Task<TierResponseDTO> CreateTierAsync(CreateTierDTO request)
        {
            var isDuplicate = await _context.Tiers.AnyAsync(t => t.TierName == request.TierName || t.MinAccumulatedPoints == request.MinAccumulatedPoints);
            if (isDuplicate) throw new BadRequestException("Tier name or minimum points requirement already exists.");

            var tier = new Tier
            {
                TierName = request.TierName,
                PointMultiplier = request.PointMultiplier,
                BookingWindowDays = request.BookingWindowDays,
                MinAccumulatedPoints = request.MinAccumulatedPoints
            };

            _context.Tiers.Add(tier);
            await _context.SaveChangesAsync();

            return await GetTierById(tier.TierId);
        }

        public async Task<TierResponseDTO> UpdateTierAsync(int id, UpdateTierDTO request)
        {
            var tier = await _context.Tiers.FindAsync(id);
            if (tier == null) throw new NotFoundException("Membership tier not found.");

            var isDuplicate = await _context.Tiers.AnyAsync(t => (t.TierName == request.TierName || t.MinAccumulatedPoints == request.MinAccumulatedPoints) && t.TierId != id);
            if (isDuplicate) throw new BadRequestException("Tier name or minimum points requirement already exists.");

            tier.TierName = request.TierName;
            tier.PointMultiplier = request.PointMultiplier;
            tier.BookingWindowDays = request.BookingWindowDays;
            tier.MinAccumulatedPoints = request.MinAccumulatedPoints;

            await _context.SaveChangesAsync();
            return await GetTierById(tier.TierId);
        }

        public async Task<TierUpgradeResultDTO?> EvaluateAndUpgradeTierAsync(int userId)
        {
            var profile = await _context.CustomerProfiles
                .Include(cp => cp.Tier)
                .FirstOrDefaultAsync(cp => cp.UserId == userId);

            if (profile == null) throw new NotFoundException("Customer profile not found.");

            var result = await EvaluateTierForProfileAsync(profile.UserId);
            if (result != null)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        public async Task<TierUpgradeResultDTO?> EvaluateTierForProfileAsync(int userId)
        {
            var profile = await _context.CustomerProfiles.Include(p => p.Tier).FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) throw new AutoWashPro.BLL.Exceptions.NotFoundException("Profile not found.");
            if (profile.Tier == null && profile.TierId > 0)
            {
                profile.Tier = await _context.Tiers.FindAsync(profile.TierId);
            }

            if (profile.Tier == null) return null;

            var eligibleTier = await _context.Tiers
                .Where(t => t.MinAccumulatedPoints <= profile.CurrentYearTierPoints)
                .OrderByDescending(t => t.MinAccumulatedPoints)
                .FirstOrDefaultAsync();

            if (eligibleTier == null || eligibleTier.TierId == profile.TierId)
                return null;

            if (eligibleTier.MinAccumulatedPoints <= profile.Tier.MinAccumulatedPoints)
                return null;

            var oldTierName = profile.Tier.TierName;
            profile.TierId = eligibleTier.TierId;

            return new TierUpgradeResultDTO
            {
                OldTierName = oldTierName,
                NewTierName = eligibleTier.TierName
            };
        }

        private async Task<TierResponseDTO> GetTierById(int id)
        {
            var t = await _context.Tiers.FindAsync(id);
            return new TierResponseDTO
            {
                TierId = t.TierId,
                TierName = t.TierName,
                PointMultiplier = t.PointMultiplier,
                BookingWindowDays = t.BookingWindowDays,
                MinAccumulatedPoints = t.MinAccumulatedPoints
            };
        }
    }
}