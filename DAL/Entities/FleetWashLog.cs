using AutoWashPro.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class FleetWashLog
    {
        public int FleetWashLogId { get; set; }
        public int FleetVehicleId { get; set; }
        public int BranchId { get; set; }
        public int? BookingId { get; set; }
        public DateTime CheckInTime { get; set; }
        public string? Status { get; set; } = "CheckedIn"; //CheckedIn, Processing, Completed, Cancelled
        public DateTime? CompletedTime { get; set; }
        public decimal WashCost { get; set; }
        public Branch Branch { get; set; } = null!;
        public FleetVehicle FleetVehicle { get; set; } = null!;
        public Booking? Booking { get; set; }
        public int? LaneId { get; set; }
        public int? StaffUserId { get; set; }
        public Lane? Lane { get; set; }
        public User? Staff { get; set; }
        public string? CheckInImageUrl { get; set; }
        public string? CheckOutImageUrl { get; set; }
    }
}
