using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoWashPro.BLL.DTOs
{
    public class StaffResponseDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Position { get; set; }
        public DateTime? HiredDate { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
    }

    public class CreateStaffDTO
    {
        [Required]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự, gồm 1 chữ hoa và 1 chữ số.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Họ tên không được chỉ chứa khoảng trắng.")]
        public string FullName { get; set; } = string.Empty;

        public string? Position { get; set; }
        public DateTime? HiredDate { get; set; }
    }

    public class UpdateStaffDTO
    {
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Họ tên không được chỉ chứa khoảng trắng.")]
        public string? FullName { get; set; }

        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? Position { get; set; }
        public DateTime? HiredDate { get; set; }
    }

    public class WorkShiftResponseDTO
    {
        public int WorkShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateWorkShiftDTO
    {
        [Required]
        [RegularExpression(@"^(?!\s*$).+", ErrorMessage = "Tên ca không được chỉ chứa khoảng trắng.")]
        public string ShiftName { get; set; } = string.Empty;

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }
    }

    public class UpdateWorkShiftDTO : CreateWorkShiftDTO
    {
        public bool IsActive { get; set; } = true;
    }

    public class ShiftAssignmentResponseDTO
    {
        public int AssignmentId { get; set; }
        public int StaffUserId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public int WorkShiftId { get; set; }
        public string ShiftName { get; set; } = string.Empty;
        public DateTime WorkDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class CreateShiftAssignmentDTO
    {
        [Required]
        public int StaffUserId { get; set; }

        [Required]
        public int WorkShiftId { get; set; }

        [Required]
        public DateTime WorkDate { get; set; }

        public string? Note { get; set; }
    }

    public class UpdateShiftAssignmentDTO
    {
        [Required]
        public int WorkShiftId { get; set; }

        [Required]
        public DateTime WorkDate { get; set; }

        [RegularExpression("^(Scheduled|Completed|Absent|Cancelled)$")]
        public string Status { get; set; } = "Scheduled";

        public string? Note { get; set; }
    }

    public class OvertimeRequestResponseDTO
    {
        public int OvertimeRequestId { get; set; }
        public int StaffUserId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public DateTime WorkDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ReviewNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateOvertimeRequestDTO
    {
        [Required]
        public DateTime WorkDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public string? Reason { get; set; }
    }

    public class ReviewRequestDTO
    {
        [Required]
        public bool IsApproved { get; set; }

        public string? ReviewNote { get; set; }
    }

    public class ShiftSwapRequestResponseDTO
    {
        public int ShiftSwapRequestId { get; set; }
        public int FromAssignmentId { get; set; }
        public int ToAssignmentId { get; set; }
        public int RequestedByUserId { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public string FromStaffName { get; set; } = string.Empty;
        public string ToStaffName { get; set; } = string.Empty;
        public DateTime FromWorkDate { get; set; }
        public DateTime ToWorkDate { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ReviewNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateShiftSwapRequestDTO
    {
        [Required]
        public int FromAssignmentId { get; set; }

        [Required]
        public int ToAssignmentId { get; set; }

        public string? Reason { get; set; }
    }
}
