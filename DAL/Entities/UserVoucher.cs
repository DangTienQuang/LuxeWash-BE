using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class UserVoucher
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [ForeignKey("Voucher")]
        public int VoucherId { get; set; }
        public Voucher Voucher { get; set; } = null!;

        public bool IsUsed { get; set; }
        public DateTime? UsedDate { get; set; }
        public int UsageCount { get; set; } = 0;
        public DateTime ReceivedDate { get; set; } = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
        public DateTime ExpiryDate { get; set; }
        public DateTime? LastUsedDate { get; set; }
        public string? TriggerKey { get; set; }
    }
}
