using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Constants
{
    public static class UserStatuses
    {
        public const string Active = "Active";
        public const string Pending = "Pending";
        public const string Blocked = "Blocked";
        public const string Deleted = "Deleted";
    }

    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Staff = "Staff";
        public const string Customer = "Customer";
    }

    public static class PointConstants
    {
        public const int VndPerSpendPoint = 100;
        public const int VndPerEarnedPoint = 1000;
        public const string CompletionReasonPrefix = "Service completion";
        public const string RefundPointsReasonPrefix = "Refund points due to booking cancellation";
    }
}
