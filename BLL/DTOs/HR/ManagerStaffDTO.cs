namespace AutoWashPro.BLL.DTOs
{
    public class ManagerStaffDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int ShiftId { get; set; }
        public string ShiftName { get; set; } = null!;
    }
}
