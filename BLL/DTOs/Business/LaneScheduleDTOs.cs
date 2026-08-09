using AutoWashPro.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.DTOs.Business
{
    public class VehicleScheduleRequest
    {
        public int FleetVehicleId { get; set; }
        public VehicleType VehicleType { get; set; } = null!;
        public List<ServicePrice> ServicePrices { get; set; } = new();
    }
    public class LaneSimState
    {
        public int LaneId { get; set; }
        public bool IsBusinessLane { get; set; }
        public DateTime FreeAt { get; set; }
    }
    public class LaneScheduleResult
    {
        public bool Success { get; private set; }
        public string? ErrorMessage { get; private set; }
        public List<VehicleAssignment> Assignments { get; private set; } = new();
        public static LaneScheduleResult Ok(List<VehicleAssignment> assignments) =>
            new() { Success = true, Assignments = assignments };
        public static LaneScheduleResult Fail(string message) =>
            new() { Success = false, ErrorMessage = message };
    }
    public class VehicleAssignment
    {
        public int FleetVehicleId { get; set; }
        public int LaneId { get; set; }
        public DateTime EstimatedStart { get; set; }
        public DateTime EstimatedEnd { get; set; }
    }
}
