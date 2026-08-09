using System;
namespace AutoWashPro.BLL.DTOs
{
    public class SwapLaneByPhoneDTO
    {
        public string TargetPhoneNumber { get; set; } = null!;
        public DateTime? Date { get; set; }
    }
}
