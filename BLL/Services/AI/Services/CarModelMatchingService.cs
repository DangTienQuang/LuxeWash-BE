using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Services
{
    public class CarModelMatchingService : ICarModelMatchingService
    {
        private readonly AutoWashDbContext _context;

        public CarModelMatchingService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<CarModelMatchResult> MatchOrCreatePendingAsync(string predictedBrand, string predictedModelName)
        {
            var brand = Normalize(predictedBrand);
            var name = Normalize(predictedModelName);

            // 1. Exact match against active, approved catalog entries
            var existing = await _context.CarModels
                .Include(c => c.VehicleType)
                .Where(c => c.IsActive)
                .FirstOrDefaultAsync(c =>
                    c.Brand.ToLower() == brand &&
                    c.Name.ToLower() == name);

            if (existing != null)
            {
                return new CarModelMatchResult
                {
                    CarModelId = existing.Id,
                    Status = existing.Status,
                    IsNewlyCreated = false,
                    VehicleTypeId = existing.VehicleTypeId,
                    VehicleTypeName = existing.VehicleType?.Name
                };
            }

            // 2. Avoid spamming duplicate Pending requests from repeated check-ins
            var pending = await _context.CarModels
                .Include(c => c.VehicleType)
                .FirstOrDefaultAsync(c =>
                    c.Status == "Pending" &&
                    c.Brand.ToLower() == brand &&
                    c.Name.ToLower() == name);

            if (pending != null)
            {
                return new CarModelMatchResult
                {
                    CarModelId = pending.Id,
                    Status = pending.Status,
                    IsNewlyCreated = false,
                    VehicleTypeId = pending.VehicleTypeId,
                    VehicleTypeName = pending.VehicleType?.Name
                };
            }

            // 3. No match anywhere -> auto-submit as a Pending request, same as a user-requested model
            var newModel = new CarModel
            {
                Brand = predictedBrand.Trim(),
                Name = predictedModelName.Trim(),
                Status = "Pending",
                IsActive = false,          // stays out of active catalog/dropdowns until staff approves
                RequestedByUserId = null,  // AI-originated, not user-originated; column is nullable
                VehicleTypeId = null       // classifier only predicts make/model, not body type — staff fills this in on approval
            };

            _context.CarModels.Add(newModel);
            await _context.SaveChangesAsync();

            return new CarModelMatchResult
            {
                CarModelId = newModel.Id,
                Status = newModel.Status,
                IsNewlyCreated = true,
                VehicleTypeId = null
            };
        }

        private static string Normalize(string value) => value.Trim().ToLower();
    }
}
