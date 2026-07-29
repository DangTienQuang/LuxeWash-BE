using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoWashPro.DAL.Entities
{
    public class Wallet
    {
        [Key]
        public int WalletId { get; set; }

        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Required]
        public decimal Balance { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        public ICollection<Transaction> Transactions { get; set; } = null!;
    }
}