using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AutoWashPro.BLL.DTOs
{
    public class VoucherResponseDTO
    {
        public int VoucherId { get; set; }
        public required string Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public int PointsRequired { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime CampaignExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedDate { get; set; }
        public int UsageCount { get; set; }
        public int MaxUsagePerUser { get; set; }
        public int RemainingUsage { get; set; }
        public decimal MinOrderAmount { get; set; }
        public bool IsActive { get; set; }
        public AutoWashPro.BLL.Enums.VoucherCampaignTypeEnum CampaignType { get; set; }
        public AutoWashPro.BLL.Enums.VoucherTypeEnum VoucherType { get; set; }
        public string? ImageUrl { get; set; }
        public int? RequiredTierId { get; set; }
        public string? RequiredTierName { get; set; }
        public TimeSpan? ValidStartTime { get; set; }
        public TimeSpan? ValidEndTime { get; set; }
        public int? VehicleTypeId { get; set; }
    }

    public class RedeemVoucherRequestDTO
    {
        [Required(ErrorMessage = "Voucher ID is required.")]
        public int VoucherId { get; set; }
    }

    public class ConsumeVoucherRequestDTO
    {
        [Required(ErrorMessage = "User ID is required.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Voucher code is required.")]
        public required string VoucherCode { get; set; }
    }

    public class AdminVoucherDTO
    {
        public int VoucherId { get; set; }
        public required string Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public int MaxUsages { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int PointsRequired { get; set; }
        public int RedeemedCount { get; set; }
        public int CurrentUsageCount { get; set; }
        public int MaxUsagePerUser { get; set; }
        public decimal MinOrderAmount { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartDate { get; set; }
        public int? ExpiryDays { get; set; }
        public AutoWashPro.BLL.Enums.VoucherCampaignTypeEnum CampaignType { get; set; }
        public AutoWashPro.BLL.Enums.VoucherTypeEnum VoucherType { get; set; }
        public string? ImageUrl { get; set; }
        public int? RequiredTierId { get; set; }
        public string? RequiredTierName { get; set; }
        public TimeSpan? ValidStartTime { get; set; }
        public TimeSpan? ValidEndTime { get; set; }
        public int? VehicleTypeId { get; set; }
    }

    public class CreateOrUpdateVoucherDTO
    {
        [Required(ErrorMessage = "Voucher code is required.")]
        [MaxLength(50)]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Voucher code cannot consist of only whitespace.")]
        public required string Code { get; set; }

        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Discount amount is invalid.")]
        public decimal DiscountAmount { get; set; }

        [Range(0, int.MaxValue)]
        public int MaxUsages { get; set; } = 0;

        [Range(1, int.MaxValue)]
        public int MaxUsagePerUser { get; set; } = 1;

        [Required]
        public DateTime ExpiryDate { get; set; }

        public DateTime? StartDate { get; set; }

        [Range(0, int.MaxValue)]
        public int PointsRequired { get; set; }

        public AutoWashPro.BLL.Enums.VoucherTypeEnum VoucherType { get; set; } = AutoWashPro.BLL.Enums.VoucherTypeEnum.Discount;
        public string? ImageUrl { get; set; }
        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Minimum order amount is invalid.")]
        public decimal MinOrderAmount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public int? RequiredTierId { get; set; }
        public TimeSpan? ValidStartTime { get; set; }
        public TimeSpan? ValidEndTime { get; set; }
        public int? VehicleTypeId { get; set; }
    }

    public abstract class CreateAutomatedVoucherBaseDTO
    {
        [Required]
        [MaxLength(50)]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Voucher code cannot consist of only whitespace.")]
        public required string Code { get; set; }

        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Discount amount is invalid.")]
        public decimal DiscountAmount { get; set; }

        [Range(0, int.MaxValue)]
        public int MaxUsages { get; set; } = 0;

        [Range(1, int.MaxValue)]
        public int MaxUsagePerUser { get; set; } = 1;

        [Range(1, 3650)]
        public int ExpiryDays { get; set; } = 7;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Minimum order amount is invalid.")]
        public decimal MinOrderAmount { get; set; } = 0;

        public string? ImageUrl { get; set; }
        public int? RequiredTierId { get; set; }
        public TimeSpan? ValidStartTime { get; set; }
        public TimeSpan? ValidEndTime { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateBirthdayVouchersDTO : CreateAutomatedVoucherBaseDTO
    {
    }

    public class CreateAgeVouchersDTO : CreateAutomatedVoucherBaseDTO
    {
        [Range(1, 150)]
        public int TargetAge { get; set; }
    }

    public class CreateWinbackVouchersDTO : CreateAutomatedVoucherBaseDTO
    {
        [Range(1, 3650)]
        public int InactiveDays { get; set; } = 60;

        [Range(1, 3650)]
        public int ResendAfterDays { get; set; } = 30;
    }

    public class CreateVipVouchersDTO : CreateAutomatedVoucherBaseDTO
    {
    }

    public class CreateMilestoneVouchersDTO : CreateAutomatedVoucherBaseDTO
    {
        [Range(1, int.MaxValue)]
        public int MilestoneUsageCount { get; set; }
    }

    public class CampaignVoucherResponseDTO
    {
        public int VoucherId { get; set; }
        public required string Code { get; set; }
        public decimal DiscountAmount { get; set; }
        public int MaxUsages { get; set; }
        public int MaxUsagePerUser { get; set; }
        public int? ExpiryDays { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MinOrderAmount { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ImageUrl { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? RequiredTierId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TimeSpan? ValidStartTime { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TimeSpan? ValidEndTime { get; set; }
        public bool IsActive { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TargetAge { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? InactiveDays { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ResendAfterDays { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MilestoneUsageCount { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ScannedUsers { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? GrantedCount { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SkippedCount { get; set; }
    }

    public class VoucherCampaignProcessResultDTO
    {
        public AutoWashPro.BLL.Enums.VoucherCampaignTypeEnum CampaignType { get; set; }
        public required string VoucherCode { get; set; }
        public int ScannedUsers { get; set; }
        public int GrantedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<VoucherCampaignGrantDTO> GrantedUsers { get; set; } = new();
    }

    public class VoucherCampaignGrantDTO
    {
        public int UserId { get; set; }
        public int VoucherId { get; set; }
        public required string VoucherCode { get; set; }
        public required string TriggerKey { get; set; }
        public DateTime ReceivedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
    }

    public class GrantVoucherRequestDTO
    {
        [Required(ErrorMessage = "User ID list is required.")]
        public List<int> UserIds { get; set; } = new();
    }
}
