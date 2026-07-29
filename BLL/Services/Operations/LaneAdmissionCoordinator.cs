using AutoWashPro.BLL.DTOs;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Operations
{
    public class LaneAdmissionCoordinator : ILaneAdmissionCoordinator
    {
        private readonly AutoWashDbContext _context;

        public LaneAdmissionCoordinator(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<GateCheckInResult> CheckInAtEntryGateAsync(
            string licensePlate,
            int branchId,
            int? bookingId = null,
            int? fleetWashLogId = null,
            int? forcedLaneId = null,
            CancellationToken cancellationToken = default)
        {
            using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            Lane? selectedLane = null;
            if (forcedLaneId.HasValue)
            {
                var isOccupied = await _context.LaneOccupancies
                    .AnyAsync(o => o.LaneId == forcedLaneId.Value, cancellationToken);
                if (!isOccupied)
                {
                    selectedLane = await _context.Lanes.FindAsync(new object[] { forcedLaneId.Value }, cancellationToken);
                }
            }
            else
            {
                var activeLanes = await _context.Lanes
                    .Where(l => l.BranchId == branchId && l.IsActive)
                    .OrderBy(l => l.Name)
                    .ToListAsync(cancellationToken);

                // Find an available lane
                foreach (var lane in activeLanes)
                {
                    var isOccupied = await _context.LaneOccupancies
                        .AnyAsync(o => o.LaneId == lane.LaneId, cancellationToken);
                    if (!isOccupied)
                    {
                        selectedLane = lane;
                        break;
                    }
                }
            }

            var result = new GateCheckInResult
            {
                BookingId = bookingId,
                FleetWashLogId = fleetWashLogId,
                LicensePlate = licensePlate
            };

            if (selectedLane != null)
            {
                // Assign lane and grant admission
                var occupancy = new LaneOccupancy
                {
                    LaneId = selectedLane.LaneId,
                    BranchId = branchId,
                    BookingId = bookingId,
                    FleetWashLogId = fleetWashLogId,
                    LicensePlate = licensePlate,
                    OccupiedAt = DateTime.UtcNow
                };
                _context.LaneOccupancies.Add(occupancy);

                // Publish admission granted & entry barrier command
                var commandId = Guid.NewGuid().ToString();
                var barrierCmd = new BarrierCommand
                {
                    CommandId = commandId,
                    BranchId = branchId,
                    BarrierId = "ENTRY_GATE",
                    Action = "OPEN",
                    BookingId = bookingId,
                    FleetWashLogId = fleetWashLogId,
                    LicensePlate = licensePlate,
                    LaneId = selectedLane.LaneId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(1),
                    Status = "Pending"
                };
                _context.BarrierCommands.Add(barrierCmd);

                // Add to Outbox for real-time Display
                var outboxMsg = new OutboxMessage
                {
                    Type = "admission_granted",
                    Payload = JsonSerializer.Serialize(new
                    {
                        LicensePlate = licensePlate,
                        LaneId = selectedLane.LaneId,
                        LaneName = selectedLane.Name,
                        BarrierCommandId = commandId,
                        BookingId = bookingId
                    }),
                    CreatedAt = DateTime.UtcNow,
                    
                };
                _context.OutboxMessages.Add(outboxMsg);

                result.Status = "Assigned";
                result.AdmissionStatus = "Granted";
                result.IsWaiting = false;
                result.LaneId = selectedLane.LaneId;
                result.LaneName = selectedLane.Name;
                result.BarrierCommandId = commandId;
                result.BarrierCommandCreated = true;
                result.Message = $"Assigned to {selectedLane.Name} and Entry Barrier OPEN command sent.";
            }
            else
            {
                // Wait in queue
                var outboxMsg = new OutboxMessage
                {
                    Type = "vehicle_waiting",
                    Payload = JsonSerializer.Serialize(new
                    {
                        LicensePlate = licensePlate,
                        BookingId = bookingId
                    }),
                    CreatedAt = DateTime.UtcNow,
                    
                };
                _context.OutboxMessages.Add(outboxMsg);

                result.Status = "Waiting";
                result.AdmissionStatus = "Denied_Queueing";
                result.IsWaiting = true;
                result.Message = "All lanes are occupied. Please wait before the barrier.";
            }

            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return result;
        }

        public async Task<CheckOutResult> CheckOutAtExitGateAsync(
            string licensePlate,
            int branchId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteCheckOutAsync(null, licensePlate, branchId, cancellationToken);
        }

        public async Task<CheckOutResult> ManualCheckOutAsync(
            int bookingId,
            int employeeId,
            CancellationToken cancellationToken = default)
        {
            var booking = await _context.Bookings.FindAsync(new object[] { bookingId }, cancellationToken);
            return await ExecuteCheckOutAsync(bookingId, booking?.ActualVehicleType?.Name ?? "", booking?.BranchId ?? 0, cancellationToken); // Using licenseplate or fallback
        }

        private async Task<CheckOutResult> ExecuteCheckOutAsync(
            int? bookingId,
            string licensePlate,
            int branchId,
            CancellationToken cancellationToken)
        {
            using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            LaneOccupancy? occupancy = null;
            if (bookingId.HasValue)
            {
                occupancy = await _context.LaneOccupancies.FirstOrDefaultAsync(o => o.BookingId == bookingId, cancellationToken);
            }
            else
            {
                occupancy = await _context.LaneOccupancies.FirstOrDefaultAsync(o => o.LicensePlate == licensePlate && o.BranchId == branchId, cancellationToken);
            }

            if (occupancy == null)
            {
                // Vehicle not currently occupying any lane
                return new CheckOutResult();
            }

            // Release Lane
            _context.LaneOccupancies.Remove(occupancy);

            // Open Exit Barrier
            var exitCmdId = Guid.NewGuid().ToString();
            var exitBarrierCmd = new BarrierCommand
            {
                CommandId = exitCmdId,
                BranchId = occupancy.BranchId,
                BarrierId = "EXIT_GATE",
                Action = "OPEN",
                BookingId = occupancy.BookingId,
                FleetWashLogId = occupancy.FleetWashLogId,
                LicensePlate = occupancy.LicensePlate,
                LaneId = occupancy.LaneId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(1),
                Status = "Pending"
            };
            _context.BarrierCommands.Add(exitBarrierCmd);

            var outboxClear = new OutboxMessage
            {
                Type = "lane_cleared",
                Payload = JsonSerializer.Serialize(new
                {
                    LaneId = occupancy.LaneId,
                    LicensePlate = occupancy.LicensePlate,
                    BarrierCommandId = exitCmdId
                }),
                CreatedAt = DateTime.UtcNow,
                
            };
            _context.OutboxMessages.Add(outboxClear);
            
            await _context.SaveChangesAsync(cancellationToken);

            var result = new CheckOutResult
            {
                CompletedBookingId = occupancy.BookingId,
                CompletedFleetWashLogId = occupancy.FleetWashLogId,
                ReleasedLaneId = occupancy.LaneId,
                ExitBarrierCommandId = exitCmdId
            };

            // Try to admit next vehicle
            var nextAdmitted = await InternalAdmitNextWaitingVehicleAsync(occupancy.LaneId, occupancy.BranchId, cancellationToken);
            if (nextAdmitted != null)
            {
                result.NextAdmission = nextAdmitted;
            }

            await tx.CommitAsync(cancellationToken);
            return result;
        }

        public async Task<AdmissionResult?> AdmitNextWaitingVehicleAsync(int laneId, CancellationToken cancellationToken = default)
        {
            using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            var lane = await _context.Lanes.FindAsync(new object[] { laneId }, cancellationToken);
            if (lane == null) return null;
            
            var result = await InternalAdmitNextWaitingVehicleAsync(laneId, lane.BranchId, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }

        private async Task<AdmissionResult?> InternalAdmitNextWaitingVehicleAsync(int laneId, int branchId, CancellationToken cancellationToken)
        {
            var isOccupied = await _context.LaneOccupancies.AnyAsync(o => o.LaneId == laneId, cancellationToken);
            if (isOccupied) return null;

            // Find oldest waiting booking (FIFO)
            var waitingBooking = await _context.Bookings
                .Include(b => b.ActualVehicleType)
                .Where(b => b.BranchId == branchId && b.Status == "CheckedIn" && b.ProcessingLaneId == null)
                .OrderBy(b => b.UpdatedAt ?? b.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            // Find oldest waiting fleet wash log (FIFO)
            var waitingFleet = await _context.FleetWashLogs
                .Include(f => f.FleetVehicle)
                .Where(f => f.BranchId == branchId && f.Status == "CheckedIn" && f.LaneId == null)
                .OrderBy(f => f.CheckInTime)
                .FirstOrDefaultAsync(cancellationToken);

            AdmissionResult? admission = null;

            if (waitingBooking != null && (waitingFleet == null || (waitingBooking.UpdatedAt ?? waitingBooking.CreatedAt) < waitingFleet.CheckInTime))
            {
                // Admit booking
                var licensePlate = waitingBooking.LicensePlate ?? "UNKNOWN"; // Usually we should fetch from Vehicle but LicensePlate might be in DTO/Booking
                // ... Oh wait, I need to check where LicensePlate is for Booking. I'll just query it.
                var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.UserId == waitingBooking.UserId, cancellationToken);
                licensePlate = vehicle?.LicensePlate ?? "UNKNOWN";
                
                admission = await GrantAdmissionAsync(laneId, branchId, waitingBooking.BookingId, null, licensePlate, cancellationToken);
                waitingBooking.ProcessingLaneId = laneId;
                waitingBooking.Status = "Processing";
            }
            else if (waitingFleet != null)
            {
                // Admit fleet
                var licensePlate = waitingFleet.FleetVehicle?.LicensePlate ?? "UNKNOWN";
                admission = await GrantAdmissionAsync(laneId, branchId, null, waitingFleet.FleetWashLogId, licensePlate, cancellationToken);
                waitingFleet.LaneId = laneId;
                waitingFleet.Status = "Processing";
            }

            if (admission != null)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return admission;
        }

        private async Task<AdmissionResult> GrantAdmissionAsync(int laneId, int branchId, int? bookingId, int? fleetId, string licensePlate, CancellationToken cancellationToken)
        {
            var lane = await _context.Lanes.FindAsync(new object[] { laneId }, cancellationToken);

            var occupancy = new LaneOccupancy
            {
                LaneId = laneId,
                BranchId = branchId,
                BookingId = bookingId,
                FleetWashLogId = fleetId,
                LicensePlate = licensePlate,
                OccupiedAt = DateTime.UtcNow
            };
            _context.LaneOccupancies.Add(occupancy);

            var cmdId = Guid.NewGuid().ToString();
            var entryCmd = new BarrierCommand
            {
                CommandId = cmdId,
                BranchId = branchId,
                BarrierId = "ENTRY_GATE",
                Action = "OPEN",
                BookingId = bookingId,
                FleetWashLogId = fleetId,
                LicensePlate = licensePlate,
                LaneId = laneId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(1),
                Status = "Pending"
            };
            _context.BarrierCommands.Add(entryCmd);

            var outboxAdmit = new OutboxMessage
            {
                Type = "admission_granted",
                Payload = JsonSerializer.Serialize(new
                {
                    LicensePlate = licensePlate,
                    LaneId = laneId,
                    LaneName = lane?.Name,
                    BarrierCommandId = cmdId,
                    BookingId = bookingId
                }),
                CreatedAt = DateTime.UtcNow
            };
            _context.OutboxMessages.Add(outboxAdmit);

            return new AdmissionResult
            {
                BookingId = bookingId,
                FleetWashLogId = fleetId,
                LicensePlate = licensePlate,
                LaneId = laneId,
                LaneName = lane?.Name,
                EntryBarrierCommandId = cmdId
            };
        }
    }
}
