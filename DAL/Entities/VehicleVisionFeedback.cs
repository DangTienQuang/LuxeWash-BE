using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class VehicleVisionFeedback
    {
        [Key]
        public int FeedbackId { get; set; }

        [Required]
        [MaxLength(20)]
        public string LicensePlate { get; set; } = string.Empty;

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public int? PredictedVehicleTypeId { get; set; }

        [Required]
        public int ActualVehicleTypeId { get; set; }

        public string? ActualBrand { get; set; }

        public string? ActualModel { get; set; }

        public int? ReportedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
    }
}
