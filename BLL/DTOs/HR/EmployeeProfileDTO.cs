namespace AutoWashPro.BLL.DTOs
{
    public class EmployeeProfileDTO
    {
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int? BranchId { get; set; }
    }
}
