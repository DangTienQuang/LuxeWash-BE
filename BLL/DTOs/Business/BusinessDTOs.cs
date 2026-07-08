using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs.Business
{
    public class BusinessProfileResponseDTO
    {
        public int BusinessProfileId { get; set; }
        public string CompanyName { get; set; } = null!;
        public string? TaxCode { get; set; }
        public string? BusinessAddress { get; set; }
        public string ApprovalStatus { get; set; } = null!;
        public string? RejectionReason { get; set; }
        public string BusinessLicenseFileUrl { get; set; } = null!;
        public string? AuthorizationLetterFileUrl { get; set; }
    }

    public class RegisterBusinessUserRequest
    {
        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Phone number is invalid.")]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Password must have at least 8 characters, including 1 uppercase letter and 1 digit.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Company name is required.")]
        [MaxLength(100)]
        public string CompanyName { get; set; } = null!;

        public string? TaxCode { get; set; }
        public string? BusinessAddress { get; set; }
        public string? BillingEmail { get; set; }
        public string? RepresentativeName { get; set; }
        public int? PaymentTermDays { get; set; }

        [Required(ErrorMessage = "Please provide the business license.")]
        public IFormFile BusinessLicense { get; set; } = null!;
        public IFormFile? AuthorizationLetter { get; set; }
    }

    public class RegisterBusinessUserResponse
    {
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string Role { get; set; } = null!;
        public int BusinessProfileId { get; set; }
        public string CompanyName { get; set; } = null!;
        public string ApprovalStatus { get; set; } = null!;
        public string BusinessLicenseFileUrl { get; set; } = null!;
        public string? AuthorizationLetterFileUrl { get; set; }
    }
}
