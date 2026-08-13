#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
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
using AutoWashPro.BLL.DTOs.Operations;

namespace AutoWashPro.BLL.Services.Operations
{
    public class LaneAdmissionCoordinator : ILaneAdmissionCoordinator
    {
        private readonly AutoWashDbContext _context;
        private readonly AutoWashPro.BLL.Services.Interface.IUserNotificationService _userNotificationService;

        public LaneAdmissionCoordinator(AutoWashDbContext context, AutoWashPro.BLL.Services.Interface.IUserNotificationService userNotificationService)
        {
            _context = context;
            _userNotificationService = userNotificationService;
        }

        private IQueryable<Lane> BuildCompatibleLaneQuery(int branchId, bool isBusiness)
        {
            return _context.Lanes
                .Where(l => l.BranchId == branchId && l.IsActive && l.IsBusinessLane == isBusiness)
                .OrderBy(l => l.Name);
        }

        public async Task<GateCheckInResult> CheckInAtEntryGateAsync(
            string licensePlate,
            int branchId,
            int? bookingId = null,
            int? fleetWashLogId = null,
            int? forcedLaneId = null,
            CancellationToken cancellationToken = default)
        {
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var tx = ownsTransaction ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;

            try
            {
                Booking? booking = null;
                FleetWashLog? fleetLog = null;
                bool isBusiness = false;
                bool isVip = false;

                if (bookingId.HasValue)
                {
                    booking = await _context.Bookings
                        .Include(b => b.User)
                            .ThenInclude(u => u.CustomerProfile)
                                .ThenInclude(cp => cp.Tier)
                        .FirstOrDefaultAsync(b => b.BookingId == bookingId.Value, cancellationToken);
                        
                    if (booking != null) 
                    {
                        isBusiness = booking.BookingType == "Business" || booking.BookingType == "Fleet";
                        var profile = booking.User?.CustomerProfile;
                        if (profile != null && (profile.TotalPoint >= 5000 
                            || string.Equals(profile.Tier?.TierName, "Gold", StringComparison.OrdinalIgnoreCase) 
                            || string.Equals(profile.Tier?.TierName, "Platinum", StringComparison.OrdinalIgnoreCase) 
                            || string.Equals(profile.Tier?.TierName, "Diamond", StringComparison.OrdinalIgnoreCase)))
                        {
                            isVip = true;
                        }
                    }
                }

                // A Business booking and its FleetWashLog describe the same physical
                // wash session. Resolve the linked log even when the caller only sends
                // BookingId so a single LaneOccupancy owns both references.
                if (booking != null && !fleetWashLogId.HasValue)
                {
                    fleetLog = await _context.FleetWashLogs
                        .FirstOrDefaultAsync(f =>
                            f.BookingId == booking.BookingId &&
                            f.Status != "Completed" &&
                            f.Status != "Cancelled",
                            cancellationToken);
                    if (fleetLog != null)
                    {
                        fleetWashLogId = fleetLog.FleetWashLogId;
                        isBusiness = true;
                    }
                }
                
                if (fleetWashLogId.HasValue)
                {
                    fleetLog = await _context.FleetWashLogs.FindAsync(new object[] { fleetWashLogId.Value }, cancellationToken);
                    isBusiness = true; // Fleet is always business
                }

                var existingOccupancy = await _context.LaneOccupancies
                    .Include(o => o.Lane)
                    .FirstOrDefaultAsync(o =>
                        (bookingId.HasValue && o.BookingId == bookingId.Value) ||
                        (fleetWashLogId.HasValue && o.FleetWashLogId == fleetWashLogId.Value),
                        cancellationToken);
                if (existingOccupancy != null)
                {
                    if (booking != null && existingOccupancy.BookingId == null)
                        existingOccupancy.BookingId = booking.BookingId;
                    if (fleetLog != null && existingOccupancy.FleetWashLogId == null)
                        existingOccupancy.FleetWashLogId = fleetLog.FleetWashLogId;
                    if (booking != null)
                        booking.ProcessingLaneId = existingOccupancy.LaneId;
                    if (fleetLog != null)
                        fleetLog.LaneId = existingOccupancy.LaneId;
                    await _context.SaveChangesAsync(cancellationToken);

                    if (ownsTransaction && tx != null)
                        await tx.CommitAsync(cancellationToken);

                    return new GateCheckInResult
                    {
                        BookingId = bookingId,
                        FleetWashLogId = fleetWashLogId,
                        LicensePlate = licensePlate,
                        Status = "Assigned",
                        AdmissionStatus = "Granted",
                        IsWaiting = false,
                        LaneId = existingOccupancy.LaneId,
                        LaneName = existingOccupancy.Lane?.Name,
                        Message = $"Already assigned to {existingOccupancy.Lane?.Name ?? "lane"}."
                    };
                }

                Lane? selectedLane = null;
                if (forcedLaneId.HasValue)
                {
                    var isOccupied = await _context.LaneOccupancies
                        .AnyAsync(o => o.LaneId == forcedLaneId.Value, cancellationToken);
                    
                    var lane = await _context.Lanes.FindAsync(new object[] { forcedLaneId.Value }, cancellationToken);
                    if (lane == null || lane.BranchId != branchId || !lane.IsActive || lane.IsBusinessLane != isBusiness)
                    {
                        throw new InvalidOperationException("LANE_UNAVAILABLE");
                    }

                    if (!isOccupied)
                    {
                        selectedLane = lane;
                    }
                }
                else
                {
                    var activeLanes = await BuildCompatibleLaneQuery(branchId, isBusiness).ToListAsync(cancellationToken);

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

                var now = DateTime.UtcNow;

                if (selectedLane != null)
                {
                    var occupancy = new LaneOccupancy
                    {
                        LaneId = selectedLane.LaneId,
                        BranchId = branchId,
                        BookingId = bookingId,
                        FleetWashLogId = fleetWashLogId,
                        LicensePlate = licensePlate,
                        OccupiedAt = now
                    };
                    _context.LaneOccupancies.Add(occupancy);

                    if (booking != null)
                    {
                        booking.ProcessingLaneId = selectedLane.LaneId;
                        booking.Status = "CheckedIn";
                        booking.ProcessingStartTime = null;
                        booking.CompletedTime = null;
                        booking.ActualDurationMinutes = null;
                        booking.UpdatedAt = now;
                    }

                    if (fleetLog != null)
                    {
                        fleetLog.LaneId = selectedLane.LaneId;
                        fleetLog.Status = "Assigned";
                    }

                    var barrierId = isVip ? "ENTRY_VIP_GATE" : "ENTRY_REGULAR_GATE";
                    var commandId = Guid.NewGuid().ToString();
                    var barrierCmd = new BarrierCommand
                    {
                        CommandId = commandId,
                        BranchId = branchId,
                        BarrierId = barrierId,
                        Action = "OPEN",
                        BookingId = bookingId,
                        FleetWashLogId = fleetWashLogId,
                        LicensePlate = licensePlate,
                        LaneId = selectedLane.LaneId,
                        CreatedAt = now,
                        ExpiresAt = now.AddMinutes(1),
                        Status = "Pending"
                    };
                    _context.BarrierCommands.Add(barrierCmd);

                    var outboxMsg = new OutboxMessage
                    {
                        Type = "admission_granted",
                        Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                        {
                            EventId = Guid.NewGuid().ToString(),
                            Type = "admission_granted",
                            BranchId = branchId,
                            OccurredAt = now,
                            Data = JsonSerializer.SerializeToElement(new
                            {
                                LicensePlate = licensePlate,
                                LaneId = selectedLane.LaneId,
                                LaneName = selectedLane.Name,
                                BarrierCommandId = commandId,
                                BarrierId = barrierId,
                                BookingId = bookingId
                            }, OperationsOutboxEnvelope.OutboxJsonOptions)
                        }, OperationsOutboxEnvelope.OutboxJsonOptions),
                        CreatedAt = now
                    };
                    _context.OutboxMessages.Add(outboxMsg);

                    var barrierOutboxMsg = new OutboxMessage
                    {
                        Type = "barrier_command",
                        Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                        {
                            EventId = Guid.NewGuid().ToString(),
                            Type = "barrier_command",
                            BranchId = branchId,
                            OccurredAt = now,
                            Data = JsonSerializer.SerializeToElement(new
                            {
                                commandId = commandId,
                                branchId = branchId,
                                barrierId = barrierId,
                                action = "OPEN",
                                bookingId = bookingId,
                                fleetWashLogId = fleetWashLogId,
                                licensePlate = licensePlate,
                                laneId = selectedLane.LaneId,
                                createdAt = barrierCmd.CreatedAt,
                                expiresAt = barrierCmd.ExpiresAt
                            }, OperationsOutboxEnvelope.OutboxJsonOptions)
                        }, OperationsOutboxEnvelope.OutboxJsonOptions),
                        CreatedAt = now
                    };
                    _context.OutboxMessages.Add(barrierOutboxMsg);

                    result.Status = "Assigned";
                    result.AdmissionStatus = "Granted";
                    result.IsWaiting = false;
                    result.LaneId = selectedLane.LaneId;
                    result.LaneName = selectedLane.Name;
                    result.BarrierCommandId = commandId;
                    result.BarrierCommandCreated = true;
                    result.BarrierId = barrierId;
                    result.BarrierCommandExpiresAt = barrierCmd.ExpiresAt;
                    result.Message = $"Assigned to {selectedLane.Name} and Entry Barrier OPEN command sent.";
                }
                else
                {
                    if (booking != null)
                    {
                        booking.Status = "CheckedIn";
                        booking.ProcessingLaneId = null;
                        booking.UpdatedAt = now;
                    }

                    if (fleetLog != null)
                    {
                        fleetLog.Status = "CheckedIn";
                        fleetLog.LaneId = null;
                    }

                    var outboxMsg = new OutboxMessage
                    {
                        Type = "vehicle_waiting",
                        Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                        {
                            EventId = Guid.NewGuid().ToString(),
                            Type = "vehicle_waiting",
                            BranchId = branchId,
                            OccurredAt = now,
                            Data = JsonSerializer.SerializeToElement(new
                            {
                                LicensePlate = licensePlate,
                                BookingId = bookingId,
                                ReasonCode = "NO_AVAILABLE_LANE",
                                Message = "Vui lòng giữ nguyên vị trí trước barie."
                            }, OperationsOutboxEnvelope.OutboxJsonOptions)
                        }, OperationsOutboxEnvelope.OutboxJsonOptions),
                        CreatedAt = now
                    };
                    _context.OutboxMessages.Add(outboxMsg);

                    result.Status = "Waiting";
                    result.AdmissionStatus = "Denied_Queueing";
                    result.IsWaiting = true;
                    result.Message = "All lanes are occupied. Please wait before the barrier.";
                }

                await _context.SaveChangesAsync(cancellationToken);
                
                if (ownsTransaction && tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch
            {
                if (ownsTransaction && tx != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }
                throw;
            }
        }

        public async Task<CheckOutResult> CheckOutAtExitGateAsync(
            string licensePlate,
            int branchId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteLaneReleaseAsync(null, licensePlate, branchId, LaneReleaseMode.PhysicalCheckout, null, cancellationToken);
        }

        public async Task<CheckOutResult> CompletePhysicalCheckoutAsync(
            int bookingId,
            int employeeId,
            CancellationToken cancellationToken = default)
        {
            var booking = await _context.Bookings.FindAsync(new object[] { bookingId }, cancellationToken);
            return await ExecuteLaneReleaseAsync(bookingId, booking?.LicensePlate ?? "", booking?.BranchId ?? 0, LaneReleaseMode.PhysicalCheckout, null, cancellationToken);
        }

        public async Task<CheckOutResult> ReleaseLaneAsync(
            int bookingId,
            string targetStatus,
            CancellationToken cancellationToken = default)
        {
            var booking = await _context.Bookings.FindAsync(new object[] { bookingId }, cancellationToken);
            return await ExecuteLaneReleaseAsync(bookingId, booking?.LicensePlate ?? "", booking?.BranchId ?? 0, LaneReleaseMode.AdministrativeRelease, targetStatus, cancellationToken);
        }

        private async Task<CheckOutResult> ExecuteLaneReleaseAsync(
            int? bookingId,
            string licensePlate,
            int branchId,
            LaneReleaseMode mode,
            string? targetStatus,
            CancellationToken cancellationToken)
        {
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var tx = ownsTransaction
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
                : null;

            try
            {
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
                    if (ownsTransaction && tx != null)
                    {
                        await tx.CommitAsync(cancellationToken);
                    }
                    return new CheckOutResult();
                }

                var now = DateTime.UtcNow;

                if (occupancy.BookingId.HasValue)
                {
                    var booking = await _context.Bookings.FindAsync(new object[] { occupancy.BookingId.Value }, cancellationToken);
                    if (booking != null)
                    {
                        booking.ProcessingLaneId = null;
                        if (mode == LaneReleaseMode.PhysicalCheckout)
                        {
                            booking.Status = "Completed";
                            booking.CompletedTime = now;
                            booking.ActualDurationMinutes = booking.ProcessingStartTime.HasValue
                                ? Math.Max(1, (int)Math.Round((now - booking.ProcessingStartTime.Value).TotalMinutes))
                                : null;
                        }
                        else
                        {
                            booking.Status = targetStatus ?? booking.Status;
                            booking.CompletedTime = null;
                        }
                        booking.UpdatedAt = now;
                    }
                }
                
                if (occupancy.FleetWashLogId.HasValue)
                {
                    var fleetLog = await _context.FleetWashLogs.FindAsync(new object[] { occupancy.FleetWashLogId.Value }, cancellationToken);
                    if (fleetLog != null)
                    {
                        fleetLog.LaneId = null;
                        if (mode == LaneReleaseMode.PhysicalCheckout)
                        {
                            fleetLog.Status = "Completed";
                            fleetLog.CompletedTime = now;
                        }
                        else
                        {
                            fleetLog.Status = targetStatus ?? fleetLog.Status;
                            fleetLog.CompletedTime = null;
                        }
                    }
                }

                _context.LaneOccupancies.Remove(occupancy);

                string? exitCmdId = null;

                if (mode == LaneReleaseMode.PhysicalCheckout)
                {
                    exitCmdId = Guid.NewGuid().ToString();
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
                        CreatedAt = now,
                        ExpiresAt = now.AddMinutes(1),
                        Status = "Pending"
                    };
                    _context.BarrierCommands.Add(exitBarrierCmd);

                    var barrierOutboxMsg = new OutboxMessage
                    {
                        Type = "barrier_command",
                        Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                        {
                            EventId = Guid.NewGuid().ToString(),
                            Type = "barrier_command",
                            BranchId = occupancy.BranchId,
                            OccurredAt = now,
                            Data = JsonSerializer.SerializeToElement(new
                            {
                                commandId = exitCmdId,
                                branchId = occupancy.BranchId,
                                barrierId = "EXIT_GATE",
                                action = "OPEN",
                                bookingId = occupancy.BookingId,
                                fleetWashLogId = occupancy.FleetWashLogId,
                                licensePlate = occupancy.LicensePlate,
                                laneId = occupancy.LaneId,
                                createdAt = exitBarrierCmd.CreatedAt,
                                expiresAt = exitBarrierCmd.ExpiresAt
                            }, OperationsOutboxEnvelope.OutboxJsonOptions)
                        }, OperationsOutboxEnvelope.OutboxJsonOptions),
                        CreatedAt = now
                    };
                    _context.OutboxMessages.Add(barrierOutboxMsg);
                }

                var outboxClear = new OutboxMessage
                {
                    Type = "lane_cleared",
                    Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                    {
                        EventId = Guid.NewGuid().ToString(),
                        Type = "lane_cleared",
                        BranchId = occupancy.BranchId,
                        OccurredAt = now,
                        Data = JsonSerializer.SerializeToElement(new
                        {
                            LaneId = occupancy.LaneId,
                            LicensePlate = occupancy.LicensePlate,
                            BarrierCommandId = exitCmdId,
                            BarrierId = exitCmdId != null ? "EXIT_GATE" : null
                        }, OperationsOutboxEnvelope.OutboxJsonOptions)
                    }, OperationsOutboxEnvelope.OutboxJsonOptions),
                    CreatedAt = now
                };
                _context.OutboxMessages.Add(outboxClear);
                
                await _context.SaveChangesAsync(cancellationToken);
                
                if (mode == LaneReleaseMode.PhysicalCheckout && occupancy.BookingId.HasValue)
                {
                    var b = await _context.Bookings.FindAsync(new object[] { occupancy.BookingId.Value }, cancellationToken);
                    if (b != null && b.UserId != null)
                    {
                        await _userNotificationService.CreateNotificationAsync(
                            b.UserId.Value,
                            "Hoàn thành dịch vụ",
                            $"Cảm ơn bạn đã sử dụng dịch vụ tại SmartWash cho biển số {b.LicensePlate}. Hẹn gặp lại bạn!",
                            "Booking",
                            b.BookingId.ToString()
                        );
                    }
                }

                var result = new CheckOutResult
                {
                    CompletedBookingId = occupancy.BookingId,
                    CompletedFleetWashLogId = occupancy.FleetWashLogId,
                    ReleasedLaneId = occupancy.LaneId,
                    ExitBarrierCommandId = exitCmdId,
                    BarrierId = exitCmdId != null ? "EXIT_GATE" : null,
                    BarrierCommandExpiresAt = exitCmdId != null ? now.AddMinutes(1) : null
                };

                var nextAdmitted = await InternalAdmitNextWaitingVehicleAsync(occupancy.LaneId, occupancy.BranchId, cancellationToken);
                if (nextAdmitted != null)
                {
                    result.NextAdmission = nextAdmitted;
                }

                if (ownsTransaction && tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch
            {
                if (ownsTransaction && tx != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }
                throw;
            }
        }

        public async Task<AdmissionResult?> AdmitNextWaitingVehicleAsync(int laneId, CancellationToken cancellationToken = default)
        {
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var tx = ownsTransaction ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
            
            try
            {
                var lane = await _context.Lanes.FindAsync(new object[] { laneId }, cancellationToken);
                if (lane == null) return null;
                
                var result = await InternalAdmitNextWaitingVehicleAsync(laneId, lane.BranchId, cancellationToken);
                
                if (ownsTransaction && tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }
                
                return result;
            }
            catch
            {
                if (ownsTransaction && tx != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }
                throw;
            }
        }

        private async Task<AdmissionResult?> InternalAdmitNextWaitingVehicleAsync(int laneId, int branchId, CancellationToken cancellationToken)
        {
            var isOccupied = await _context.LaneOccupancies.AnyAsync(o => o.LaneId == laneId, cancellationToken);
            if (isOccupied) return null;

            var lane = await _context.Lanes.FindAsync(new object[] { laneId }, cancellationToken);
            if (lane == null || !lane.IsActive) return null;

            var waitingBookings = await _context.Bookings
                .Include(b => b.Vehicle)
                .Include(b => b.User)
                    .ThenInclude(u => u.CustomerProfile)
                        .ThenInclude(cp => cp.Tier)
                .Where(b => b.BranchId == branchId && b.Status == "CheckedIn" && b.ProcessingLaneId == null 
                         && (lane.IsBusinessLane ? (b.BookingType == "Business" || b.BookingType == "Fleet") : (b.BookingType != "Business" && b.BookingType != "Fleet")))
                .ToListAsync(cancellationToken);

            var waitingBookingObj = waitingBookings
                .Select(b => 
                {
                    var profile = b.User?.CustomerProfile;
                    bool isVip = profile != null && (profile.TotalPoint >= 5000 
                        || string.Equals(profile.Tier?.TierName, "Gold", StringComparison.OrdinalIgnoreCase) 
                        || string.Equals(profile.Tier?.TierName, "Platinum", StringComparison.OrdinalIgnoreCase) 
                        || string.Equals(profile.Tier?.TierName, "Diamond", StringComparison.OrdinalIgnoreCase));
                    
                    return new
                    {
                        Booking = b,
                        LicensePlate = b.LicensePlate ?? b.Vehicle?.LicensePlate,
                        IsVip = isVip,
                        WaitTime = b.UpdatedAt ?? b.CreatedAt
                    };
                })
                .OrderByDescending(x => x.IsVip)
                .ThenBy(x => x.WaitTime)
                .FirstOrDefault();

            var waitingFleet = await _context.FleetWashLogs
                .Include(f => f.FleetVehicle)
                .Where(f => f.BranchId == branchId && f.Status == "CheckedIn" &&
                            f.LaneId == null && f.BookingId == null && lane.IsBusinessLane)
                .OrderBy(f => f.CheckInTime)
                .FirstOrDefaultAsync(cancellationToken);

            AdmissionResult? admission = null;
            var now = DateTime.UtcNow;

            if (waitingBookingObj != null && (waitingFleet == null || waitingBookingObj.WaitTime < waitingFleet.CheckInTime))
            {
                if (string.IsNullOrEmpty(waitingBookingObj.LicensePlate) || waitingBookingObj.LicensePlate == "UNKNOWN")
                {
                    return null;
                }

                var linkedFleetLog = await _context.FleetWashLogs
                    .FirstOrDefaultAsync(f =>
                        f.BookingId == waitingBookingObj.Booking.BookingId &&
                        f.Status != "Completed" && f.Status != "Cancelled",
                        cancellationToken);
                admission = await GrantAdmissionAsync(
                    laneId,
                    branchId,
                    waitingBookingObj.Booking.BookingId,
                    linkedFleetLog?.FleetWashLogId,
                    waitingBookingObj.LicensePlate,
                    waitingBookingObj.IsVip,
                    cancellationToken);
                waitingBookingObj.Booking.ProcessingLaneId = laneId;
                waitingBookingObj.Booking.Status = "CheckedIn";
                waitingBookingObj.Booking.ProcessingStartTime = null;
                waitingBookingObj.Booking.UpdatedAt = now;
                if (linkedFleetLog != null)
                {
                    linkedFleetLog.LaneId = laneId;
                    linkedFleetLog.Status = "Assigned";
                }
            }
            else if (waitingFleet != null)
            {
                var licensePlate = waitingFleet.FleetVehicle?.LicensePlate;
                if (string.IsNullOrEmpty(licensePlate) || licensePlate == "UNKNOWN")
                {
                    return null;
                }

                admission = await GrantAdmissionAsync(laneId, branchId, null, waitingFleet.FleetWashLogId, licensePlate, false, cancellationToken);
                waitingFleet.LaneId = laneId;
                waitingFleet.Status = "Assigned";
            }

            if (admission != null)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return admission;
        }

        private async Task<AdmissionResult> GrantAdmissionAsync(int laneId, int branchId, int? bookingId, int? fleetId, string licensePlate, bool isVip, CancellationToken cancellationToken)
        {
            var lane = await _context.Lanes.FindAsync(new object[] { laneId }, cancellationToken);
            var now = DateTime.UtcNow;

            var occupancy = new LaneOccupancy
            {
                LaneId = laneId,
                BranchId = branchId,
                BookingId = bookingId,
                FleetWashLogId = fleetId,
                LicensePlate = licensePlate,
                OccupiedAt = now
            };
            _context.LaneOccupancies.Add(occupancy);

            var barrierId = isVip ? "ENTRY_VIP_GATE" : "ENTRY_REGULAR_GATE";
            var cmdId = Guid.NewGuid().ToString();
            var entryCmd = new BarrierCommand
            {
                CommandId = cmdId,
                BranchId = branchId,
                BarrierId = barrierId,
                Action = "OPEN",
                BookingId = bookingId,
                FleetWashLogId = fleetId,
                LicensePlate = licensePlate,
                LaneId = laneId,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(1),
                Status = "Pending"
            };
            _context.BarrierCommands.Add(entryCmd);

            var outboxAdmit = new OutboxMessage
            {
                Type = "admission_granted",
                Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                {
                    EventId = Guid.NewGuid().ToString(),
                    Type = "assigned",
                    BranchId = branchId,
                    OccurredAt = now,
                    Data = JsonSerializer.SerializeToElement(new
                    {
                        LicensePlate = licensePlate,
                        LaneId = laneId,
                        LaneName = lane?.Name,
                        BarrierCommandId = cmdId,
                        BarrierId = barrierId,
                        BookingId = bookingId
                    }, OperationsOutboxEnvelope.OutboxJsonOptions)
                }, OperationsOutboxEnvelope.OutboxJsonOptions),
                CreatedAt = now
            };
            _context.OutboxMessages.Add(outboxAdmit);

            var barrierOutboxMsg = new OutboxMessage
            {
                Type = "barrier_command",
                Payload = JsonSerializer.Serialize(new OperationsOutboxEnvelope
                {
                    EventId = Guid.NewGuid().ToString(),
                    Type = "barrier_command",
                    BranchId = branchId,
                    OccurredAt = now,
                    Data = JsonSerializer.SerializeToElement(new
                    {
                        commandId = cmdId,
                        branchId = branchId,
                        barrierId = barrierId,
                        action = "OPEN",
                        bookingId = bookingId,
                        fleetWashLogId = fleetId,
                        licensePlate = licensePlate,
                        laneId = laneId,
                        createdAt = entryCmd.CreatedAt,
                        expiresAt = entryCmd.ExpiresAt
                    }, OperationsOutboxEnvelope.OutboxJsonOptions)
                }, OperationsOutboxEnvelope.OutboxJsonOptions),
                CreatedAt = now
            };
            _context.OutboxMessages.Add(barrierOutboxMsg);

            return new AdmissionResult
            {
                BookingId = bookingId,
                FleetWashLogId = fleetId,
                LicensePlate = licensePlate,
                LaneId = laneId,
                LaneName = lane?.Name,
                EntryBarrierCommandId = cmdId,
                BarrierId = barrierId
            };
        }
    }
}

#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
