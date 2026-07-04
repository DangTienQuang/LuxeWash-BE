using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class CustomerFeatureProfile
    {
        [Key]
        public int FeatureProfileId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual User Customer { get; set; } = null!;

        // ===== Visit Statistics =====
        public int VisitCount { get; set; }
        public int CompletedVisitCount { get; set; }
        public int CancelledVisitCount { get; set; }
        public int NoShowCount { get; set; }
        public int DaysSinceLastVisit { get; set; }
        public double AverageVisitGap { get; set; }
        public int LongestVisitGap { get; set; }
        public int ShortestVisitGap { get; set; }
        [MaxLength(50)]
        public string VisitTrend { get; set; } = "Stable";

        // ===== Spending =====

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageSpend { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal HighestSpend { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal LowestSpend { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal LifetimeSpend { get; set; }
        public int LifetimeBookings { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageDiscountReceived { get; set; }
        public decimal AverageOriginalSpend { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalVoucherSavings { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPointSavings { get; set; }

        // ===== Preferences =====

        public int? FavoriteServiceId { get; set; }
        public virtual Service? FavoriteService { get; set; }
        public int? FavoriteServiceUsage { get; set; }
        public double? AverageServicesPerBooking { get; set; }
        public int TotalServicesPurchased { get; set; }
        public int? FavoriteBranchId { get; set; }
        public virtual Branch? FavoriteBranch { get; set; }
        [MaxLength(20)]
        public string? FavoriteVisitDay { get; set; }
        public int? FavoriteVisitHour { get; set; }
        public int? BasicServiceBookings { get; set; }
        public double? PremiumServiceRate { get; set; }

        // ===== Behaviour =====
        public double WeekendVisitRate { get; set; }
        public double MorningVisitRate { get; set; }
        public double AfternoonVisitRate { get; set; }
        public double EveningVisitRate { get; set; }
        public double RainVisitRate { get; set; }

        // ===== Branch Preference
        public int FavoriteBranchVisits { get; set; }
        public double BranchLoyaltyRate { get; set; }

        // ===== Promotion =====

        public double CouponUsageRate { get; set; }
        public double PointUsageRate { get; set; }
        public double PromotionResponseRate { get; set; }
        public int? MembershipTierId { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageVoucherDiscount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AveragePointDiscount { get; set; }
        public int VoucherBookings { get; set; }
        public int PointBookings { get; set; }
        public virtual Tier? MembershipTier { get; set; }
        public int CurrentPoints { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; }

        // ===== Vehicle =====

        public int VehicleCount { get; set; }
        public int PreferredVehicleTypeId { get; set; }
        public int DifferentVehicleTypes { get; set; }
        public double FleetUsageRate { get; set; }
        public double VehicleTypeConsistency { get; set; }

        // ===== Customer =====

        public double AverageRating { get; set; }
        public int ReferralCount { get; set; }
        public double ReferralSuccessRate { get; set; }

        // ===== AI Features =====

        public double PriceSensitivityScore { get; set; }
        public double PremiumPreferenceScore { get; set; }
        public double LoyaltyScore { get; set; }
        public double EngagementScore { get; set; }
        public double PredictedChurnScore { get; set; }
        public double PredictedUpgradeScore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PredictedLifetimeValue { get; set; }
        public DateTime? ExpectedNextVisit { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastFeatureCalculation { get; set; } = DateTime.UtcNow;
    }
}