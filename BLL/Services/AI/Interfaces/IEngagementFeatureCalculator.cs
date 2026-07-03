using BLL.Services.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Interfaces
{
    public interface IEngagementFeatureCalculator
    {
        Task CalculateAsync(CustomerAnalyticsSnapshot snapshot);
    }
}
