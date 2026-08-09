namespace AutoWashPro.BLL.DTOs
{
    public class BranchDTO
    {
        public int BranchId { get; set; }
        public string Name { get; set; } = null!;
        public string? Address { get; set; }
        public bool IsActive { get; set; }
    }
}
