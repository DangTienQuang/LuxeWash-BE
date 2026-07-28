using System;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using BLL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AutoWashPro.BLL.Services.Operations
{
    public class LaneAssignmentCoordinator : ILaneAssignmentCoordinator
    {
        private readonly AutoWashDbContext _context;

        public LaneAssignmentCoordinator(AutoWashDbContext context)
        {
            _context = context;
        }

        private void EnqueueOutboxEvent(LaneDisplayEventDTO eventDto)
        {
            var msg = new OutboxMessage
            {
                Type = "LaneDisplayEvent",
                Payload = JsonSerializer.Serialize(eventDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                CreatedAt = DateTime.UtcNow
            };
            _context.OutboxMessages.Add(msg);
        }

        public async Task AssignLaneForBookingAsync(int bookingId, int laneId)
        {
            var booking = await _context.Bookings.Include(b => b.Vehicle).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking != null)
            {
                booking.ProcessingLaneId = laneId;
                var lane = await _context.Lanes.FindAsync(laneId);
                
                await PublishAssignedAsync(booking.BranchId, bookingId, booking.Vehicle?.LicensePlate, laneId, lane?.Name ?? "");
            }
        }

        public Task PublishWaitingAsync(int branchId, int bookingId, string? licensePlate)
        {
            EnqueueOutboxEvent(new LaneDisplayEventDTO
            {
                BranchId = branchId,
                Type = "waiting",
                BookingId = bookingId,
                LicensePlate = licensePlate,
                ReasonCode = "NO_AVAILABLE_LANE",
                Message = "Chưa có làn trống. Vui lòng giữ nguyên vị trí trước barie.",
                DisplayUntil = DateTime.UtcNow.AddSeconds(20)
            });
            return Task.CompletedTask;
        }

        public Task PublishAssignedAsync(int branchId, int bookingId, string? licensePlate, int laneId, string laneName)
        {
            EnqueueOutboxEvent(new LaneDisplayEventDTO
            {
                BranchId = branchId,
                Type = "assigned",
                BookingId = bookingId,
                LicensePlate = licensePlate,
                LaneId = laneId,
                LaneName = laneName,
                DisplayUntil = DateTime.UtcNow.AddSeconds(15)
            });
            return Task.CompletedTask;
        }

        public Task PublishProcessingAsync(int branchId, int bookingId, string? licensePlate, int laneId, string laneName)
        {
            EnqueueOutboxEvent(new LaneDisplayEventDTO
            {
                BranchId = branchId,
                Type = "processing",
                BookingId = bookingId,
                LicensePlate = licensePlate,
                LaneId = laneId,
                LaneName = laneName,
                DisplayUntil = DateTime.UtcNow.AddSeconds(15)
            });
            return Task.CompletedTask;
        }

        public Task PublishClearedAsync(int branchId, int laneId, string laneName)
        {
            EnqueueOutboxEvent(new LaneDisplayEventDTO
            {
                BranchId = branchId,
                Type = "cleared",
                LaneId = laneId,
                LaneName = laneName
            });
            return Task.CompletedTask;
        }
    }
}
