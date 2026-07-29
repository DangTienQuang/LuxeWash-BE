using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.DAL.Data;

namespace AutoWashPro.BLL.Services.Operations
{
    public class OperationsMonitoringService : IOperationsMonitoringService
    {
        private readonly AutoWashDbContext _context;

        public OperationsMonitoringService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<QueueMonitoringDashboardDTO> GetQueueMonitoringAsync(int branchId, CancellationToken cancellationToken = default)
        {
            var occupancies = await _context.LaneOccupancies
                .Where(o => o.BranchId == branchId)
                .Select(o => new LaneOccupancyDTO
                {
                    LaneId = o.LaneId,
                    LicensePlate = o.LicensePlate,
                    BookingId = o.BookingId,
                    OccupiedAt = o.OccupiedAt,
                    LaneName = _context.Lanes.Where(l => l.LaneId == o.LaneId).Select(l => l.Name).FirstOrDefault() ?? ""
                })
                .ToListAsync(cancellationToken);

            var waitingBookings = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.Status == "CheckedIn" && b.ProcessingLaneId == null)
                .OrderBy(b => b.ScheduledTime)
                .Select(b => new WaitingVehicleDTO
                {
                    BookingId = b.BookingId,
                    LicensePlate = b.LicensePlate,
                    Status = b.Status,
                    ScheduledTime = b.ScheduledTime
                })
                .ToListAsync(cancellationToken);

            var waitingFleetLogs = await _context.FleetWashLogs
                .Include(f => f.FleetVehicle)
                .Where(f => f.BranchId == branchId && f.Status == "CheckedIn" && f.LaneId == null)
                .OrderBy(f => f.CheckInTime)
                .Select(f => new WaitingVehicleDTO
                {
                    BookingId = f.BookingId,
                    LicensePlate = f.FleetVehicle.LicensePlate,
                    Status = f.Status,
                    ScheduledTime = f.CheckInTime
                })
                .ToListAsync(cancellationToken);

            waitingBookings.AddRange(waitingFleetLogs);
            waitingBookings = waitingBookings.OrderBy(x => x.ScheduledTime).ToList();

            return new QueueMonitoringDashboardDTO
            {
                OccupiedLanes = occupancies,
                WaitingQueue = waitingBookings
            };
        }

        public async Task<List<BarrierCommandDTO>> GetFailedOrExpiredBarrierCommandsAsync(int branchId, CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow;

            return await _context.BarrierCommands
                .Where(c => c.BranchId == branchId && (c.Status == "Failed" || (c.Status == "Pending" && c.ExpiresAt < cutoff)))
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new BarrierCommandDTO
                {
                    CommandId = c.CommandId,
                    BarrierId = c.BarrierId,
                    Action = c.Action,
                    LicensePlate = c.LicensePlate,
                    LaneId = c.LaneId,
                    CreatedAt = c.CreatedAt,
                    ExpiresAt = c.ExpiresAt,
                    Status = c.Status
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ReconciliationAlertDTO>> RunReconciliationCheckAsync(int branchId, CancellationToken cancellationToken = default)
        {
            var alerts = new List<ReconciliationAlertDTO>();
            var now = DateTime.UtcNow;

            // 1. Alert when Booking Processing but no LaneOccupancy
            var processingBookings = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.Status == "Processing")
                .ToListAsync(cancellationToken);

            var currentOccupancies = await _context.LaneOccupancies
                .Where(o => o.BranchId == branchId)
                .ToListAsync(cancellationToken);

            foreach (var booking in processingBookings)
            {
                if (!currentOccupancies.Any(o => o.BookingId == booking.BookingId))
                {
                    alerts.Add(new ReconciliationAlertDTO
                    {
                        AlertType = "Missing_Occupancy",
                        Description = $"Booking {booking.BookingId} is Processing but has no physical LaneOccupancy.",
                        BookingId = booking.BookingId,
                        LicensePlate = booking.LicensePlate,
                        LaneId = booking.ProcessingLaneId,
                        DetectedAt = now
                    });
                }
            }

            // 2. Alert when LaneOccupancy exists but booking is not Processing/CheckedIn
            foreach (var occupancy in currentOccupancies)
            {
                if (occupancy.BookingId.HasValue)
                {
                    var booking = await _context.Bookings.FindAsync(new object[] { occupancy.BookingId.Value }, cancellationToken);
                    if (booking != null && booking.Status != "Processing" && booking.Status != "CheckedIn")
                    {
                        alerts.Add(new ReconciliationAlertDTO
                        {
                            AlertType = "Ghost_Occupancy",
                            Description = $"Lane {occupancy.LaneId} is occupied by Booking {booking.BookingId} which is in status '{booking.Status}'.",
                            BookingId = booking.BookingId,
                            LicensePlate = booking.LicensePlate,
                            LaneId = occupancy.LaneId,
                            DetectedAt = now
                        });
                    }
                }
            }

            return alerts;
        }
    }
}
