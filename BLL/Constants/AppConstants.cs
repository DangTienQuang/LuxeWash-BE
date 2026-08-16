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

    public static class BookingStatuses
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string CheckedIn = "CheckedIn";
        public const string Processing = "Processing";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string Delayed = "Delayed";
        public const string CancelledBySystem = "CancelledBySystem";

        private static readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
        {
            Pending, Confirmed, CheckedIn, Processing, Completed, Cancelled, Delayed, CancelledBySystem
        };

        public static bool IsAllowed(string status) => status != null && _allowed.Contains(status);

        public static bool CanTransitionFrom(string from, string to) => to switch
        {
            CheckedIn => from is Pending or Confirmed,
            Processing => from == CheckedIn,
            Completed => from is CheckedIn or Processing,
            Cancelled or Delayed => from is Pending or CheckedIn,
            Pending or CancelledBySystem => true,
            _ => false
        };
    }
}
