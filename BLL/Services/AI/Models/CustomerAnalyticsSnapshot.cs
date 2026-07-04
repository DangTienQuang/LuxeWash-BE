using AutoWashPro.DAL.Entities;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.AI.Models
{
    public class CustomerAnalyticsSnapshot
    {
        public User Customer { get; set; } = null!;
        public List<Booking> Bookings { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();
        public List<UserVoucher> UserVouchers { get; set; } = new();
        public List<PointLedger> PointLedgers { get; set; } = new();
        public List<Vehicle> Vehicles { get; set; } = new();
        public Wallet? Wallet { get; set; }
        public Tier? Tier { get; set; }
        public CustomerFeatureProfile FeatureProfile { get; set; } = null!;
    }
}
