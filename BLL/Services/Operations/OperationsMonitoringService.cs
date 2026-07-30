#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
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

            var expiredCommands = await _context.BarrierCommands
                .Where(x => x.BranchId == branchId
                    && x.Status == "Pending"
                    && x.ExpiresAt <= now)
                .ToListAsync(cancellationToken);

            foreach (var command in expiredCommands)
            {
                command.Status = "Expired";
            }
            await _context.SaveChangesAsync(cancellationToken);

            // 0. Dọn stale LaneOccupancy: xóa occupancy không còn hợp lệ
            var occupanciesToCheck = await _context.LaneOccupancies
                .Where(o => o.BranchId == branchId)
                .ToListAsync(cancellationToken);

            var staleOccupancyLaneIds = new List<int>();

            if (occupanciesToCheck.Count > 0)
            {
                // Batch load tất cả bookings liên quan trong 1 query – tránh N+1
                var occupancyBookingIds = occupanciesToCheck
                    .Where(o => o.BookingId.HasValue)
                    .Select(o => o.BookingId!.Value)
                    .Distinct()
                    .ToList();

                // Dùng Dictionary<int, (string Status, int? ProcessingLaneId)> để lookup O(1)
                var bookingLookup = occupancyBookingIds.Count > 0
                    ? (await _context.Bookings
                        .Where(b => occupancyBookingIds.Contains(b.BookingId))
                        .Select(b => new { b.BookingId, b.Status, b.ProcessingLaneId })
                        .ToListAsync(cancellationToken))
                        .ToDictionary(
                            b => b.BookingId,
                            b => (Status: b.Status, ProcessingLaneId: b.ProcessingLaneId))
                    : new Dictionary<int, (string Status, int? ProcessingLaneId)>();

                foreach (var occupancy in occupanciesToCheck)
                {
                    bool shouldDelete = false;
                    string reason = string.Empty;

                    if (!occupancy.BookingId.HasValue)
                    {
                        shouldDelete = true;
                        reason = "No BookingId";
                    }
                    else if (!bookingLookup.TryGetValue(occupancy.BookingId.Value, out var bk))
                    {
                        shouldDelete = true;
                        reason = $"Booking {occupancy.BookingId} does not exist";
                    }
                    else if (bk.Status == "Completed" || bk.Status == "Cancelled" || bk.Status == "NoShow")
                    {
                        shouldDelete = true;
                        reason = $"Booking {occupancy.BookingId} is in terminal status '{bk.Status}'";
                    }
                    else if (bk.Status != "Processing")
                    {
                        shouldDelete = true;
                        reason = $"Booking {occupancy.BookingId} status '{bk.Status}' is not Processing";
                    }
                    else if (bk.ProcessingLaneId.HasValue && bk.ProcessingLaneId.Value != occupancy.LaneId)
                    {
                        shouldDelete = true;
                        reason = $"Booking {occupancy.BookingId} ProcessingLaneId={bk.ProcessingLaneId} differs from occupancy LaneId={occupancy.LaneId}";
                    }

                    if (shouldDelete)
                    {
                        alerts.Add(new ReconciliationAlertDTO
                        {
                            AlertType = "Stale_Occupancy_Cleared",
                            Description = $"Removed stale LaneOccupancy for lane {occupancy.LaneId}. Reason: {reason}",
                            BookingId = occupancy.BookingId,
                            LicensePlate = occupancy.LicensePlate,
                            LaneId = occupancy.LaneId,
                            DetectedAt = now
                        });
                        staleOccupancyLaneIds.Add(occupancy.LaneId);
                        _context.LaneOccupancies.Remove(occupancy);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            // Sau khi dọn occupancy → thử nhận xe tiếp theo vào làn vừa giải phóng
            foreach (var laneId in staleOccupancyLaneIds.Distinct())
            {
                try
                {
                    var admission = await _laneCoordinator.AdmitNextWaitingVehicleAsync(laneId, cancellationToken);
                    if (admission != null)
                    {
                        alerts.Add(new ReconciliationAlertDTO
                        {
                            AlertType = "Empty_Lane_Admitted",
                            Description = $"After clearing stale occupancy, lane {laneId} admitted waiting vehicle {admission.LicensePlate}.",
                            BookingId = admission.BookingId,
                            LicensePlate = admission.LicensePlate,
                            LaneId = laneId,
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception)
                {
                    // Ignore errors during admission, continue to next lane
                }
            }

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
                    if (booking.Status != "Completed")
                    {
                        booking.ProcessingStartTime = null;
                        booking.CompletedTime = null;
                        booking.ActualDurationMinutes = null;
                    }
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

            // 3. Requeue failed outbox messages and expire old barrier commands
            var failedMessages = await _context.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount >= 3)
                .ToListAsync(cancellationToken);

            foreach (var msg in failedMessages)
            {
                if (msg.Type == "barrier_command")
                {
                    try
                    {
                        var envelope = System.Text.Json.JsonSerializer.Deserialize<AutoWashPro.BLL.DTOs.Operations.OperationsOutboxEnvelope>(msg.Payload, AutoWashPro.BLL.DTOs.Operations.OperationsOutboxEnvelope.OutboxJsonOptions);
                        if (envelope != null && envelope.Data.ValueKind != System.Text.Json.JsonValueKind.Undefined && envelope.Data.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            var commandIdElement = envelope.Data.GetProperty("commandId");
                            if (commandIdElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var commandId = commandIdElement.GetString();
                                var cmd = await _context.BarrierCommands.FirstOrDefaultAsync(c => c.CommandId == commandId, cancellationToken);
                                if (cmd != null && cmd.Status == "Pending")
                                {
                                    if (cmd.ExpiresAt < now)
                                    {
                                        cmd.Status = "Expired";
                                        msg.ProcessedAt = now; // Don't process it anymore
                                        msg.ErrorMessage = "Expired before it could be published.";
                                        
                                        alerts.Add(new ReconciliationAlertDTO
                                        {
                                            AlertType = "Barrier_Expired",
                                            Description = $"Barrier command {commandId} was Pending but passed ExpiresAt. Marked as Expired.",
                                            LaneId = cmd.LaneId,
                                            LicensePlate = cmd.LicensePlate,
                                            DetectedAt = now
                                        });
                                    }
                                    else
                                    {
                                        // Still valid, requeue
                                        msg.RetryCount = 0;
                                        msg.NextRetryAt = now;
                                        msg.ErrorMessage = null;
                                        
                                        alerts.Add(new ReconciliationAlertDTO
                                        {
                                            AlertType = "Barrier_Requeued",
                                            Description = $"Requeued failed barrier command {commandId}.",
                                            LaneId = cmd.LaneId,
                                            LicensePlate = cmd.LicensePlate,
                                            DetectedAt = now
                                        });
                                    }
                                }
                                else
                                {
                                    // Command no longer pending or doesn't exist, mark processed
                                    msg.ProcessedAt = now;
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // JSON parsing error or something else, ignore
                    }
                }
                else if (msg.Type == "vehicle_waiting" || msg.Type == "lane_cleared" || msg.Type == "admission_granted" || msg.Type == "assigned")
                {
                    // Do not requeue stale display events, just drop them
                    msg.ProcessedAt = now;
                    msg.ErrorMessage = "Dropped stale display event after 3 retries.";
                    
                    alerts.Add(new ReconciliationAlertDTO
                    {
                        AlertType = "Display_Event_Dropped",
                        Description = $"Dropped stale display event of type {msg.Type}.",
                        DetectedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return alerts;
        }

        public async Task<bool> AckBarrierCommandAsync(string commandId, string? status)
        {
            var command = await _context.BarrierCommands
                .FirstOrDefaultAsync(c => c.CommandId == commandId);

            if (command == null) return false;

            command.Status = string.IsNullOrWhiteSpace(status) ? "Completed" : status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsEmployeeInBranchAsync(int userId, int branchId)
        {
            var profile = await _context.EmployeeProfiles.FindAsync(userId);
            return profile != null && profile.BranchId == branchId;
        }
    }
}

#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
