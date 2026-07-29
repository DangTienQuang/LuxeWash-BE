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
        private readonly ILaneAdmissionCoordinator _laneCoordinator;

        public OperationsMonitoringService(AutoWashDbContext context, ILaneAdmissionCoordinator laneCoordinator)
        {
            _context = context;
            _laneCoordinator = laneCoordinator;
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

            // 1. Fix stale ProcessingLaneId assignments (Booking has ProcessingLaneId but no LaneOccupancy)
            var bookingsWithLane = await _context.Bookings
                .Where(b => b.BranchId == branchId && b.ProcessingLaneId != null)
                .ToListAsync(cancellationToken);

            var currentOccupancies = await _context.LaneOccupancies
                .Where(o => o.BranchId == branchId)
                .ToListAsync(cancellationToken);

            foreach (var booking in bookingsWithLane)
            {
                if (!currentOccupancies.Any(o => o.BookingId == booking.BookingId))
                {
                    alerts.Add(new ReconciliationAlertDTO
                    {
                        AlertType = "Stale_Assignment_Cleared",
                        Description = $"Booking {booking.BookingId} had ProcessingLaneId {booking.ProcessingLaneId} but no occupancy. Fields cleared.",
                        BookingId = booking.BookingId,
                        LicensePlate = booking.LicensePlate,
                        LaneId = booking.ProcessingLaneId,
                        DetectedAt = now
                    });

                    // Fix it
                    booking.ProcessingLaneId = null;
                    if (booking.Status == "Processing") 
                    {
                        booking.Status = "CheckedIn";
                    }
                    booking.ProcessingStartTime = null;
                    booking.CompletedTime = null;
                    booking.ActualDurationMinutes = null;
                }
            }
            await _context.SaveChangesAsync(cancellationToken);

            // 2. Admit next waiting vehicle for empty active lanes
            var activeLanes = await _context.Lanes
                .Where(l => l.BranchId == branchId && l.IsActive)
                .ToListAsync(cancellationToken);

            // Re-fetch occupancies in case of concurrent changes
            var freshOccupancies = await _context.LaneOccupancies
                .Where(o => o.BranchId == branchId)
                .Select(o => o.LaneId)
                .ToListAsync(cancellationToken);

            foreach (var lane in activeLanes)
            {
                if (!freshOccupancies.Contains(lane.LaneId))
                {
                    // Lane is empty, try to admit
                    try
                    {
                        var admission = await _laneCoordinator.AdmitNextWaitingVehicleAsync(lane.LaneId, cancellationToken);
                        if (admission != null)
                        {
                            alerts.Add(new ReconciliationAlertDTO
                            {
                                AlertType = "Empty_Lane_Admitted",
                                Description = $"Empty lane {lane.LaneId} automatically admitted waiting vehicle {admission.LicensePlate}.",
                                BookingId = admission.BookingId,
                                LicensePlate = admission.LicensePlate,
                                LaneId = lane.LaneId,
                                DetectedAt = DateTime.UtcNow
                            });
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors during reconciliation, move to next lane
                    }
                }
            }

            return alerts;
        }
    }
}
