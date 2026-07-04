using BLL.Services.AI.Interfaces;
using BLL.Services.AI.Models;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Calculators
{
    public class VehicleFeatureCalculator : IVehicleFeatureCalculator
    {
        public Task CalculateAsync(CustomerAnalyticsSnapshot snapshot)
        {
            var profile = snapshot.FeatureProfile;

            CalculateVehicleCount(profile, snapshot);

            return Task.CompletedTask;
        }

        private void CalculateVehicleCount(CustomerFeatureProfile profile, CustomerAnalyticsSnapshot snapshot)
        {
            profile.VehicleCount = snapshot.Vehicles.Count(v => !v.IsDeleted);
        }
    }
}
