using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class Voucher
    {
        [Key]
        public int VoucherId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; }

        public decimal DiscountAmount { get; set; }
        public int MaxUsages { get; set; }
        public int CurrentUsageCount { get; set; } = 0;
        public int MaxUsagePerUser { get; set; } = 1;

        public DateTime ExpiryDate { get; set; }
        public int? ExpiryDays { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public int PointsRequired { get; set; }

        public AutoWashPro.DAL.Enums.VoucherType VoucherType { get; set; } = AutoWashPro.DAL.Enums.VoucherType.Discount;
        public AutoWashPro.DAL.Enums.VoucherCampaignType CampaignType { get; set; } = AutoWashPro.DAL.Enums.VoucherCampaignType.Manual;

        public string? ImageUrl { get; set; }
        public decimal MinOrderAmount { get; set; } = 0;

        public int? RequiredTierId { get; set; }

        [ForeignKey("RequiredTierId")]
        public Tier? RequiredTier { get; set; }

        public int? VehicleTypeId { get; set; }
        [ForeignKey("VehicleTypeId")]
        public VehicleType? VehicleType { get; set; }

        public TimeSpan? ValidStartTime { get; set; }
        public TimeSpan? ValidEndTime { get; set; }
        public int? TargetAge { get; set; }
        public int? InactiveDays { get; set; }
        public int? ResendAfterDays { get; set; }
        public int? MilestoneUsageCount { get; set; }

        public int? BranchId { get; set; }
        [ForeignKey("BranchId")]
        public Branch? Branch { get; set; }

        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "Approved"; // "Approved", "Proposed", "Rejected"

        [MaxLength(500)]
        public string? ProposalNote { get; set; }
    }
}
