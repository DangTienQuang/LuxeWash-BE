using System;
using System.Collections.Generic;

namespace AutoWashPro.BLL.DTOs
{
    public class SuggestedBranchInfoDTO
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double DistanceKm { get; set; }
        public double OccupancyRate { get; set; }
        public int AvailableSlotsCount { get; set; }
    }

    public class SwitchBranchIncentiveVoucherDTO
    {
        public int VoucherId { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public decimal DiscountPercentage { get; set; }
        public string Description { get; set; } = string.Empty;
        public int ExpiresInHours { get; set; }
    }

    public class CheckSlotsWithSuggestionResponseDTO
    {
        public int CurrentBranchId { get; set; }
        public string CurrentBranchName { get; set; } = string.Empty;
        public double CurrentOccupancyRate { get; set; }
        public bool IsOverloaded { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public List<TimeSlotResponseDTO> TimeSlots { get; set; } = new List<TimeSlotResponseDTO>();
        public bool HasAlternativeSuggestion { get; set; }
        public SuggestedBranchInfoDTO? SuggestedAlternative { get; set; }
        public SwitchBranchIncentiveVoucherDTO? IncentiveVoucher { get; set; }
    }
}
