using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class UserProfileDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string TierName { get; set; }
        public int TotalPoint { get; set; }
        public int PromotionPoint { get; set; }
        public double ChurnScore { get; set; }
        public List<VehicleDTO> Vehicles { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Email { get; internal set; }
        public string Status { get; internal set; }
    }

    public class VehicleDTO
    {
        public string LicensePlate { get; set; }
        public int VehicleTypeId { get; set; }
        public string VehicleType { get; set; }
        public string? RegistrationPhotoUrl { get; set; }
        public string? CarModel { get; set; }
        public string? Brand { get; set; }
        public string? UserNote { get; set; }
    }

    public class CreateVehicleDTO
    {
        [Required(ErrorMessage = "License plate is required.")]
        [RegularExpression(@"^[0-9]{2}[A-Z0-9]-[0-9]{3,5}(\.[0-9]{2})?$", ErrorMessage = "License plate format is invalid (e.g., 51H-123.45).")]
        public string LicensePlate { get; set; }

        public int? VehicleTypeId { get; set; }

        public string? RegistrationPhotoUrl { get; set; }
        public IFormFile? PhotoFile { get; set; }
        public string? UserNote { get; set; }
        public int? CarModelId { get; set; }
        public string? CarModel { get; set; }
    }

    public class UpdateVehicleDTO
    {
        public int? VehicleTypeId { get; set; }
        public IFormFile? PhotoFile { get; set; }
        public string? UserNote { get; set; }
        public int? CarModelId { get; set; }
        public string? CarModel { get; set; }
    }

    public class UpdateUserProfileDTO
    {
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Full name cannot consist of only whitespace.")]
        public string? FullName { get; set; }

        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Phone number is invalid.")]
        public string? PhoneNumber { get; set; }
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string? Email { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    public class UpdateUserStatusDTO
    {
        [Required(ErrorMessage = "Status is required.")]
        [RegularExpression("^(Active|Blocked)$", ErrorMessage = "Status must be 'Active' or 'Blocked'.")]
        public string Status { get; set; }
    }

    public class PagedResultDTO<T>
    {
        public List<T> Items { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
    }

    public class UserAdminSummaryDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string TierName { get; set; }
        public string Status { get; set; }
        public DateTime? LastVisitDate { get; set; }
        public string? Email { get; internal set; }
    }

    public class VehicleRecognitionDTO
    {
        public string LicensePlate { get; set; }
        public string VehicleType { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhone { get; set; }
        public string TierName { get; set; }
        public bool HasActiveBooking { get; set; }
        public int? ActiveBookingId { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public string? CarModel { get; set; }
    }

    public class AdminOtherVehicleDTO
    {
        public string LicensePlate { get; set; }
        public int VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; }
        public int? UserId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public string? RegistrationPhotoUrl { get; set; }
        public string? UserNote { get; set; }
        public string? CarModel { get; set; }
    }

    public class UpdateVehicleTypeAdminDTO
    {
        [Required(ErrorMessage = "Please select a vehicle type.")]
        public int VehicleTypeId { get; set; }
    }

    public class ApproveVehicleTypeRequestDTO
    {
        [StringLength(50, ErrorMessage = "Vehicle type name cannot exceed 50 characters.")]
        public string? CustomizedTypeName { get; set; }

        public string? Description { get; set; }
    }
}