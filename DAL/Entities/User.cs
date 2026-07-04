using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoWashPro.DAL.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; }
        [MaxLength(100)]
        public string? Email { get; set; }
        [Required]
        public required string PasswordHash { get; set; }

        [Required]
        [MaxLength(20)]
        public required string Role { get; set; }

        [Required]
        [MaxLength(20)]
        public required string Status { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        [MaxLength(128)]
        public string? EmailVerificationOtpHash { get; set; }
        public DateTime? EmailVerificationOtpExpiresAt { get; set; }
        public CustomerProfile CustomerProfile { get; set; } = null!;
        public StaffProfile? StaffProfile { get; set; }
        public ManagerProfile? ManagerProfile { get; set; }
        public EmployeeProfile? EmployeeProfile { get; set; }
        public BusinessProfile? BusinessProfile { get; set; }
        public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
        public ICollection<AIConversationLog> AIConversationLogs { get; set; }
        public ICollection<StaffLaneAssignment> LaneAssignments { get; set; } = new List<StaffLaneAssignment>();
        public ICollection<Booking> ProcessedBookings { get; set; } = new List<Booking>();
    }
}
