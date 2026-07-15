using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BLL.Helpers;
using BLL.Services.Interface;
using BLL.DTOs.Business;

namespace AutoWashPro.BLL.Services
{
    public class ManagerService : IManagerService
    {
        private readonly AutoWashDbContext _context;
        private readonly IBranchRevenueAnalyticsService _branchRevenueAnalyticsService;

        public ManagerService(AutoWashDbContext context, IBranchRevenueAnalyticsService branchRevenueAnalyticsService)
        {
            _context = context;
            _branchRevenueAnalyticsService = branchRevenueAnalyticsService;
        }

        private async Task<EmployeeProfile> GetManagerProfileAsync(int managerUserId)
        {
            var profile = await _context.EmployeeProfiles
                .FirstOrDefaultAsync(e => e.EmployeeId == managerUserId);

            if (profile == null)
            {
                throw new BadRequestException("Manager profile not found.");
            }

            if (!profile.BranchId.HasValue)
            {
                throw new BadRequestException("Manager is not assigned to any branch.");
            }

            return profile;
        }

        public async Task<List<ManagerStaffDTO>> GetStaffInBranchAsync(int managerUserId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var staffList = await _context.EmployeeProfiles
                .Include(e => e.User)
                .Where(e => e.BranchId == managerProfile.BranchId && e.User.Role == "Staff")
                .Select(e => new ManagerStaffDTO
                {
                    UserId = e.EmployeeId,
                    FullName = e.FullName,
                    PhoneNumber = e.User.PhoneNumber,
                    Status = e.User.Status
                })
                .ToListAsync();

            return staffList;
        }

        public async Task<bool> AssignStaffToLaneAsync(int managerUserId, AssignStaffToLaneDTO assignDto)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            // Verify staff belongs to manager's branch
            var staffProfile = await _context.EmployeeProfiles
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmployeeId == assignDto.StaffId && e.BranchId == managerProfile.BranchId && e.User.Role == "Staff");

            if (staffProfile == null)
            {
                throw new BadRequestException("Staff not found in your branch.");
            }

            // Verify lane belongs to manager's branch
            var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == assignDto.LaneId && l.BranchId == managerProfile.BranchId);
            if (lane == null)
            {
                throw new BadRequestException("Lane not found in your branch.");
            }

            // Verify staff has a scheduled work shift on that Date and WorkShiftId
            var hasScheduledShift = await _context.StaffShiftAssignments
                .AnyAsync(s => s.StaffUserId == staffProfile.EmployeeId && s.WorkDate.Date == assignDto.AssignedDate.Date && s.WorkShiftId == assignDto.WorkShiftId);

            if (!hasScheduledShift)
            {
                throw new BadRequestException("Employee does not have a work schedule in this shift on the selected date.");
            }

            // Verify staff is not already assigned to ANY lane for the exact same WorkShiftId on the exact same Date
            var existingAssignment = await _context.StaffLaneAssignments
                .AnyAsync(a => a.StaffId == assignDto.StaffId && a.AssignedDate.Date == assignDto.AssignedDate.Date && a.WorkShiftId == assignDto.WorkShiftId);

            if (existingAssignment)
            {
                throw new BadRequestException("This employee is already assigned to another lane in this shift.");
            }

            var assignment = new StaffLaneAssignment
            {
                StaffId = assignDto.StaffId,
                LaneId = assignDto.LaneId,
                AssignedDate = assignDto.AssignedDate.Date,
                WorkShiftId = assignDto.WorkShiftId
            };

            _context.StaffLaneAssignments.Add(assignment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnassignStaffFromLaneAsync(int managerUserId, int laneId, int staffId, System.DateTime? date = null)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            // Verify the Lane belongs to managerProfile.BranchId
            var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == laneId && l.BranchId == managerProfile.BranchId);
            if (lane == null)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            var targetDate = date?.Date ?? System.DateTime.UtcNow.ToVnTime().Date;

            // Find the active shift assignment for targetDate
            var assignment = await _context.StaffLaneAssignments
                .FirstOrDefaultAsync(a => a.LaneId == laneId && a.StaffId == staffId && a.AssignedDate.Date == targetDate);

            if (assignment == null)
            {
                throw new NotFoundException("Active staff assignment not found for the specified date in this lane.");
            }

            _context.StaffLaneAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ManagerBookingListDTO>> GetCheckInBookingsInBranchAsync(int managerUserId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var bookings = await _context.Bookings
                .Include(b => b.User)
                    .ThenInclude(u => u.CustomerProfile)
                .Include(b => b.BookingDetails)
                    .ThenInclude(d => d.Service)
                .Include(b => b.ProcessingLane)
                .Include(b => b.ProcessingStaff)
                    .ThenInclude(s => s.EmployeeProfile)
                .Where(b => b.BranchId == managerProfile.BranchId && (b.Status == "CheckedIn" || b.Status == "Pending" || b.Status == "Processing"))
                .ToListAsync();

            return bookings.Select(b => new ManagerBookingListDTO
            {
                BookingId = b.BookingId,
                Status = b.Status,
                ScheduledTime = b.ScheduledTime,
                CustomerName = b.User?.CustomerProfile?.FullName,
                CustomerPhone = b.User?.PhoneNumber,
                LicensePlate = b.LicensePlate,
                ServiceNames = b.BookingDetails.Select(d => d.Service.ServiceName).ToList(),
                ProcessingLaneId = b.ProcessingLaneId,
                ProcessingLaneName = b.ProcessingLane?.Name,
                ProcessingStaffId = b.ProcessingStaffId,
                ProcessingStaffName = b.ProcessingStaff?.EmployeeProfile?.FullName,
                IsBusinessLane = b.ProcessingLane != null && b.ProcessingLane.IsBusinessLane,
                ProcessingStartTime = b.ProcessingStartTime.HasValue ? b.ProcessingStartTime.Value.ToVnTime() : (DateTime?)null,
                CompletedTime = b.CompletedTime.HasValue ? b.CompletedTime.Value.ToVnTime() : (DateTime?)null,
                ActualDurationMinutes = b.ActualDurationMinutes
            }).ToList();
        }

        public async Task<List<TimeSlotAdminResponseDTO>> GetTimeSlotsInBranchAsync(int managerUserId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var timeSlots = await _context.TimeSlots
                .Where(ts => ts.BranchId == managerProfile.BranchId)
                .OrderBy(ts => ts.StartTime)
                .Select(ts => new TimeSlotAdminResponseDTO
                {
                    SlotId = ts.SlotId,
                    BranchId = ts.BranchId,
                    StartTime = ts.StartTime,
                    EndTime = ts.EndTime,
                    MaxCapacity = ts.MaxCapacity,
                    IsVipOnly = ts.IsVipOnly
                })
                .ToListAsync();

            return timeSlots;
        }

        public async Task<LaneDTO> CreateLaneAsync(int managerUserId, CreateLaneDTO request)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            // Override BranchId to manager's branch
            request.BranchId = managerProfile.BranchId!.Value;

            var lane = new Lane
            {
                Name = request.Name,
                BranchId = request.BranchId,
                IsBusinessLane = request.IsBusinessLane,
                IsActive = true
            };

            _context.Lanes.Add(lane);
            await _context.SaveChangesAsync();

            return new LaneDTO
            {
                LaneId = lane.LaneId,
                Name = lane.Name,
                BranchId = lane.BranchId,
                IsActive = lane.IsActive,
                IsBusinessLane = lane.IsBusinessLane
            };
        }

        public async Task<TimeSlotAdminResponseDTO> CreateTimeSlotAsync(int managerUserId, CreateTimeSlotDTO request)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            // Override BranchId to manager's branch
            request.BranchId = managerProfile.BranchId!.Value;

            if (request.StartTime >= request.EndTime)
            {
                throw new BadRequestException("Start time must be earlier than end time.");
            }

            // Kiểm tra trùng lặp thời gian trong chi nhánh của manager
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

        public async Task<List<LaneStaffAssignmentDTO>> GetLanesInBranchAsync(int managerUserId, System.DateTime? date = null)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            var targetDate = date?.Date ?? System.DateTime.UtcNow.ToVnTime().Date;

            var lanes = await _context.Lanes
                .Where(l => l.BranchId == managerProfile.BranchId)
                .Select(l => new LaneStaffAssignmentDTO
                {
                    LaneId = l.LaneId,
                    Name = l.Name,
                    BranchId = l.BranchId,
                    IsActive = l.IsActive,
                    IsBusinessLane = l.IsBusinessLane
                })
                .ToListAsync();

            var laneIds = lanes.Select(l => l.LaneId).ToList();
            var assignments = await _context.StaffLaneAssignments
                .Include(a => a.Staff)
                    .ThenInclude(s => s.EmployeeProfile)
                .Include(a => a.WorkShift)
                .Where(a => laneIds.Contains(a.LaneId) && a.AssignedDate.Date == targetDate)
                .ToListAsync();

            foreach (var lane in lanes)
            {
                lane.AssignedStaff = assignments.Where(a => a.LaneId == lane.LaneId).Select(a => new ManagerStaffDTO
                {
                    UserId = a.Staff.UserId,
                    FullName = a.Staff.EmployeeProfile!.FullName,
                    PhoneNumber = a.Staff.PhoneNumber,
                    Status = a.Staff.Status,
                    ShiftId = a.WorkShiftId,
                    ShiftName = a.WorkShift.ShiftName
                }).ToList();
            }
            return lanes;
        }

        public async Task<List<ManagerStaffDTO>> GetStaffAssignedToLaneAsync(int managerUserId, int laneId, System.DateTime? date = null)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == laneId && l.BranchId == managerProfile.BranchId);
            if (lane == null)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            var targetDate = date?.Date ?? System.DateTime.UtcNow.ToVnTime().Date;

            var assignments = await _context.StaffLaneAssignments
                .Include(a => a.Staff)
                    .ThenInclude(s => s.EmployeeProfile)
                .Include(a => a.WorkShift)
                .Where(a => a.LaneId == laneId && a.AssignedDate.Date == targetDate)
                .ToListAsync();

            return assignments.Select(a => new ManagerStaffDTO
            {
                UserId = a.Staff.UserId,
                FullName = a.Staff.EmployeeProfile!.FullName,
                PhoneNumber = a.Staff.PhoneNumber,
                Status = a.Staff.Status,
                ShiftId = a.WorkShiftId,
                ShiftName = a.WorkShift.ShiftName
            }).ToList();
        }

        public async Task<bool> ConfirmCheckInAndAssignLaneAsync(int managerUserId, int bookingId, AssignBookingToLaneDTO assignment)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.BranchId == managerProfile.BranchId);

            if (booking == null)
            {
                throw new NotFoundException("Booking not found in your branch.");
            }

            if (booking.Status != "Pending" && booking.Status != "CheckedIn")
            {
                throw new BadRequestException("Booking is not in a valid state for check-in and assignment.");
            }

            var validLane = await _context.Lanes
                .AnyAsync(l => l.LaneId == assignment.LaneId && l.BranchId == managerProfile.BranchId);

            if (!validLane)
            {
                throw new BadRequestException("Lane is invalid or does not belong to your branch.");
            }

            booking.ProcessingLaneId = assignment.LaneId;
            booking.Status = "CheckedIn";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<LaneDTO> UpdateLaneAsync(int managerUserId, int laneId, UpdateLaneDTO request)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == laneId);
            if (lane == null || lane.BranchId != managerProfile.BranchId)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            request.BranchId = managerProfile.BranchId.Value;

            lane.Name = request.Name;
            lane.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            return new LaneDTO
            {
                LaneId = lane.LaneId,
                Name = lane.Name,
                BranchId = lane.BranchId,
                IsActive = lane.IsActive
            };
        }

        public async Task<bool> DeleteLaneAsync(int managerUserId, int laneId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var lane = await _context.Lanes.FirstOrDefaultAsync(l => l.LaneId == laneId);
            if (lane == null || lane.BranchId != managerProfile.BranchId)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            lane.IsActive = false;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<TimeSlotAdminResponseDTO> UpdateTimeSlotAsync(int managerUserId, int slotId, UpdateTimeSlotDTO request)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var timeSlot = await _context.TimeSlots.FindAsync(slotId);
            if (timeSlot == null || timeSlot.BranchId != managerProfile.BranchId)
            {
                throw new NotFoundException("Time slot not found in your branch.");
            }

            request.BranchId = managerProfile.BranchId.Value;

            if (request.StartTime >= request.EndTime)
            {
                throw new BadRequestException("Start time must be earlier than end time.");
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

        public async Task<bool> DeleteTimeSlotAsync(int managerUserId, int slotId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var timeSlot = await _context.TimeSlots.FindAsync(slotId);
            if (timeSlot == null || timeSlot.BranchId != managerProfile.BranchId)
            {
                throw new NotFoundException("Time slot not found in your branch.");
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

        public async Task<bool> DeactivateStaffAsync(int managerUserId, int staffUserId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);

            var staffProfile = await _context.EmployeeProfiles
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmployeeId == staffUserId && e.BranchId == managerProfile.BranchId && e.User.Role == "Staff");

            if (staffProfile == null)
            {
                throw new NotFoundException("Staff not found in your branch.");
            }

            if (staffProfile.User.Status == "Inactive")
            {
                return true;
            }

            staffProfile.User.Status = "Inactive";
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<MonthlyRevenueCampaignResultDTO> CheckRevenueStimulusCampaignAsync(int managerUserId, int? month = null, int? year = null)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            return await _branchRevenueAnalyticsService.CheckAndTriggerMonthlyRevenueCampaignAsync(managerProfile.BranchId!.Value, month, year);
        }

        public async Task<List<VoucherProposalDTO>> GetPendingProposalsAsync(int managerUserId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            return await _branchRevenueAnalyticsService.GetPendingProposalsAsync(managerProfile.BranchId!.Value);
        }

        public async Task<VoucherProposalDTO> ModifyProposalAsync(int managerUserId, int voucherId, ModifyVoucherProposalDTO dto)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            return await _branchRevenueAnalyticsService.ModifyProposalAsync(managerProfile.BranchId!.Value, voucherId, dto);
        }

        public async Task<MonthlyRevenueCampaignResultDTO> ApproveProposalAsync(int managerUserId, int voucherId)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            return await _branchRevenueAnalyticsService.ApproveProposalAsync(managerProfile.BranchId!.Value, voucherId);
        }

        public async Task<bool> RejectProposalAsync(int managerUserId, int voucherId, string? rejectReason)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            return await _branchRevenueAnalyticsService.RejectProposalAsync(managerProfile.BranchId!.Value, voucherId, rejectReason);
        }

        public async Task<BranchComprehensiveStimulusDTO> GenerateComprehensiveStimulusAnalysisAsync(int managerUserId, int? month = null, int? year = null)
        {
            var managerProfile = await GetManagerProfileAsync(managerUserId);
            return await _branchRevenueAnalyticsService.GenerateComprehensiveStimulusAnalysisAsync(managerProfile.BranchId!.Value, month, year);
        }
    }
}
