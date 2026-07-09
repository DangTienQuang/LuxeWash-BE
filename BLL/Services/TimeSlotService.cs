using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class TimeSlotService : ITimeSlotService
    {
        private readonly AutoWashDbContext _context;

        public TimeSlotService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<TimeSlotAdminResponseDTO>> GetAllTimeSlotsAsync(int? branchId = null)
        {
            var query = _context.TimeSlots.AsQueryable();
            if (branchId.HasValue)
            {
                query = query.Where(ts => ts.BranchId == branchId.Value);
            }

            return await query
                .OrderBy(ts => ts.StartTime)
                .Select(ts => new TimeSlotAdminResponseDTO
                {
                    SlotId = ts.SlotId,
                    BranchId = ts.BranchId,
                    StartTime = ts.StartTime,
                    EndTime = ts.EndTime,
                    MaxCapacity = ts.MaxCapacity,
                    IsVipOnly = ts.IsVipOnly
                }).ToListAsync();
        }

        public async Task<TimeSlotAdminResponseDTO> CreateTimeSlotAsync(CreateTimeSlotDTO request)
        {
            if (request.StartTime >= request.EndTime)
            {
                throw new BadRequestException("Start time must be earlier than end time.");
            }

            // Kiểm tra trùng lặp thời gian
            var isOverlap = await _context.TimeSlots.AnyAsync(ts =>
                ts.BranchId == request.BranchId &&
                ((request.StartTime >= ts.StartTime && request.StartTime < ts.EndTime) ||
                (request.EndTime > ts.StartTime && request.EndTime <= ts.EndTime) ||
                (request.StartTime <= ts.StartTime && request.EndTime >= ts.EndTime)));

            if (isOverlap)
            {
                throw new BadRequestException("Time slot overlaps with an existing time slot.");
            }

            var timeSlot = new TimeSlot
            {
                BranchId = request.BranchId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                MaxCapacity = request.MaxCapacity,
                IsVipOnly = request.IsVipOnly
            };

            _context.TimeSlots.Add(timeSlot);
            await _context.SaveChangesAsync();

            return new TimeSlotAdminResponseDTO
            {
                SlotId = timeSlot.SlotId,
                BranchId = timeSlot.BranchId,
                StartTime = timeSlot.StartTime,
                EndTime = timeSlot.EndTime,
                MaxCapacity = timeSlot.MaxCapacity,
                IsVipOnly = timeSlot.IsVipOnly
            };
        }

        public async Task<TimeSlotAdminResponseDTO> UpdateTimeSlotAsync(int slotId, UpdateTimeSlotDTO request)
        {
            if (request.StartTime >= request.EndTime)
            {
                throw new BadRequestException("Start time must be earlier than end time.");
            }

            var timeSlot = await _context.TimeSlots.FindAsync(slotId);
            if (timeSlot == null)
            {
                throw new NotFoundException("Time slot not found.");
            }

            var isOverlap = await _context.TimeSlots.AnyAsync(ts =>
                ts.SlotId != slotId &&
                ts.BranchId == request.BranchId &&
                ((request.StartTime >= ts.StartTime && request.StartTime < ts.EndTime) ||
                 (request.EndTime > ts.StartTime && request.EndTime <= ts.EndTime) ||
                 (request.StartTime <= ts.StartTime && request.EndTime >= ts.EndTime)));

            if (isOverlap)
            {
                throw new BadRequestException("Time slot overlaps with an existing time slot.");
            }

            timeSlot.BranchId = request.BranchId;
            timeSlot.StartTime = request.StartTime;
            timeSlot.EndTime = request.EndTime;
            timeSlot.MaxCapacity = request.MaxCapacity;
            timeSlot.IsVipOnly = request.IsVipOnly;

            await _context.SaveChangesAsync();

            return new TimeSlotAdminResponseDTO
            {
                SlotId = timeSlot.SlotId,
                BranchId = timeSlot.BranchId,
                StartTime = timeSlot.StartTime,
                EndTime = timeSlot.EndTime,
                MaxCapacity = timeSlot.MaxCapacity,
                IsVipOnly = timeSlot.IsVipOnly
            };
        }

        public async Task<bool> DeleteTimeSlotAsync(int slotId)
        {
            var timeSlot = await _context.TimeSlots.FindAsync(slotId);
            if (timeSlot == null)
            {
                throw new NotFoundException("Time slot not found.");
            }

            var isBooked = await _context.DailySlotCapacities.AnyAsync(dsc => dsc.SlotId == slotId && dsc.BookedWeight > 0);
            if (isBooked)
            {
                throw new BadRequestException("Cannot delete this time slot because there are existing bookings. Please check again.");
            }

            _context.TimeSlots.Remove(timeSlot);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
