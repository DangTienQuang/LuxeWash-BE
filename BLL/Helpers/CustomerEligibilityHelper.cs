using System;
using AutoWashPro.DAL.Entities;

namespace BLL.Helpers
{
    public static class CustomerEligibilityHelper
    {
        public static bool IsVipEligible(CustomerProfile? profile)
        {
            if (profile == null)
            {
                return false;
            }

            var tierName = profile.Tier?.TierName;
            return profile.TotalPoint >= 5000
                || string.Equals(tierName, "Gold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tierName, "Platinum", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tierName, "Diamond", StringComparison.OrdinalIgnoreCase);
        }
    }
}
