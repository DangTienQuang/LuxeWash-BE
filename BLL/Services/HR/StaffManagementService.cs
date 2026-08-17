#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
using AutoWashPro.BLL.Constants;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
namespace AutoWashPro.BLL.Services
{
    public class StaffManagementService : IStaffManagementService
    {
        private readonly AutoWashDbContext _context;
        public StaffManagementService(AutoWashDbContext context)
        {
            _context = context;
        }
        public async Task<List<StaffResponseDTO>> GetStaffsAsync(string? keyword, string? role, string? status)
        {
            var query = _context.Users
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile).ThenInclude(e => e.Branch)
                .Where(u => u.Role == UserRoles.Staff || u.Role == UserRoles.Manager)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var key = keyword.Trim().ToLower();
                query = query.Where(u => u.PhoneNumber.Contains(key)
                    || (u.Email != null && u.Email.ToLower().Contains(key))
                    || (u.StaffProfile != null && u.StaffProfile.FullName.ToLower().Contains(key))
                    || (u.ManagerProfile != null && u.ManagerProfile.FullName.ToLower().Contains(key)));
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role.Trim());
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(u => u.Status == status.Trim());
            }
            var users = await query
                .OrderBy(u => u.Role == UserRoles.Manager ? u.ManagerProfile!.FullName : u.StaffProfile!.FullName)
                .ToListAsync();
            return users.Select(MapStaff).ToList();
        }
        public async Task<List<StaffResponseDTO>> GetStaffsByRoleAsync(string role, string? keyword, string? status)
        {
            EnsurePersonnelRole(role);
            return await GetStaffsAsync(keyword, role, status);
        }
        public async Task<StaffResponseDTO> GetStaffByRoleAsync(int staffUserId, string role)
        {
            EnsurePersonnelRole(role);
            var user = await GetStaffUserAsync(staffUserId);
            if (user.Role != role) throw new NotFoundException("Staff not found.");
            return MapStaff(user);
        }
        public async Task<StaffResponseDTO> CreateStaffAsync(CreateStaffDTO request)
        {
            return await CreatePersonnelAsync(request, UserRoles.Staff);
        }
        public async Task<StaffResponseDTO> CreateStaffWithRoleAsync(CreateStaffDTO request, string role)
        {
            EnsurePersonnelRole(role);
            return await CreatePersonnelAsync(request, role);
        }
        private async Task<StaffResponseDTO> CreatePersonnelAsync(CreateStaffDTO request, string role)
        {
            EnsurePersonnelRole(role);
            var phone = request.PhoneNumber.Trim();
            var exists = await _context.Users.AnyAsync(u => u.PhoneNumber == phone);
            if (exists) throw new BadRequestException("This phone number is already registered.");
            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();
            if (email != null && await _context.Users.AnyAsync(u => u.Email == email))
                throw new BadRequestException("This email is already in use.");
            var user = new User
            {
                PhoneNumber = phone,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                Status = UserStatuses.Active
            };
            if (role == UserRoles.Manager)
            {
                user.ManagerProfile = new ManagerProfile
                {
                    FullName = request.FullName.Trim(),
                    Position = request.Position?.Trim(),
                    HiredDate = request.HiredDate?.Date ?? AutoWashPro.DAL.Helpers.TimeHelper.VnNow.Date
                };
            }
            else
            {
                user.StaffProfile = new StaffProfile
                {
                    FullName = request.FullName.Trim(),
                    Position = request.Position?.Trim(),
                    HiredDate = request.HiredDate?.Date ?? AutoWashPro.DAL.Helpers.TimeHelper.VnNow.Date
                };
            }
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return MapStaff(user);
        }
        public async Task<StaffResponseDTO> UpdateStaffAsync(int staffUserId, UpdateStaffDTO request)
        {
            var user = await GetStaffUserAsync(staffUserId);
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phone = request.PhoneNumber.Trim();
                if (await _context.Users.AnyAsync(u => u.UserId != staffUserId && u.PhoneNumber == phone))
                    throw new BadRequestException("This phone number is already in use.");
                user.PhoneNumber = phone;
            }
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var email = request.Email.Trim().ToLower();
                if (await _context.Users.AnyAsync(u => u.UserId != staffUserId && u.Email == email))
                    throw new BadRequestException("This email is already in use.");
                user.Email = email;
            }
            if (user.Role == UserRoles.Manager)
            {
                user.ManagerProfile ??= new ManagerProfile { UserId = staffUserId, FullName = request.FullName?.Trim() ?? user.PhoneNumber };
                if (!string.IsNullOrWhiteSpace(request.FullName))
                    user.ManagerProfile.FullName = request.FullName.Trim();
                user.ManagerProfile.Position = request.Position?.Trim();
                if (request.HiredDate.HasValue)
                    user.ManagerProfile.HiredDate = request.HiredDate.Value.Date;
            }
            else
            {
                user.StaffProfile ??= new StaffProfile { UserId = staffUserId, FullName = request.FullName?.Trim() ?? user.PhoneNumber };
                if (!string.IsNullOrWhiteSpace(request.FullName))
                    user.StaffProfile.FullName = request.FullName.Trim();
                user.StaffProfile.Position = request.Position?.Trim();
                if (request.HiredDate.HasValue)
                    user.StaffProfile.HiredDate = request.HiredDate.Value.Date;
            }
            await _context.SaveChangesAsync();
            return MapStaff(user);
        }
        public async Task<StaffResponseDTO> UpdateStaffByRoleAsync(int staffUserId, string role, UpdateStaffDTO request)
        {
            EnsurePersonnelRole(role);
            var user = await GetStaffUserAsync(staffUserId);
            if (user.Role != role) throw new NotFoundException("Staff not found.");
            return await UpdateStaffAsync(staffUserId, request);
        }
        public async Task<bool> UpdateStaffStatusAsync(int staffUserId, string status)
        {
            if (status != UserStatuses.Active && status != UserStatuses.Blocked)
                throw new BadRequestException("Status can only be Active or Blocked.");
            var user = await GetStaffUserAsync(staffUserId);
            user.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> SoftDeleteStaffByRoleAsync(int staffUserId, string role)
        {
            EnsurePersonnelRole(role);
            var user = await GetStaffUserAsync(staffUserId);
            if (user.Role != role) throw new NotFoundException("Staff not found.");
            user.Status = UserStatuses.Blocked;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<WorkShiftResponseDTO>> GetWorkShiftsAsync(bool includeInactive)
        {
            var query = _context.WorkShifts.AsQueryable();
            if (!includeInactive) query = query.Where(s => s.IsActive);
            return await query.OrderBy(s => s.StartTime).Select(s => MapWorkShift(s)).ToListAsync();
        }
        public async Task<WorkShiftResponseDTO> CreateWorkShiftAsync(CreateWorkShiftDTO request)
        {
            ValidateTimeRange(request.StartTime, request.EndTime);
            var name = request.ShiftName.Trim();
            if (await _context.WorkShifts.AnyAsync(s => s.ShiftName.ToLower() == name.ToLower()))
                throw new BadRequestException("Shift name already exists.");
            var shift = new WorkShift
            {
                ShiftName = name,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsActive = true
            };
            _context.WorkShifts.Add(shift);
            await _context.SaveChangesAsync();
            return MapWorkShift(shift);
        }
        public async Task<WorkShiftResponseDTO> UpdateWorkShiftAsync(int workShiftId, UpdateWorkShiftDTO request)
        {
            ValidateTimeRange(request.StartTime, request.EndTime);
            var shift = await _context.WorkShifts.FindAsync(workShiftId);
            if (shift == null) throw new NotFoundException("Work shift not found.");
            var name = request.ShiftName.Trim();
            if (await _context.WorkShifts.AnyAsync(s => s.WorkShiftId != workShiftId && s.ShiftName.ToLower() == name.ToLower()))
                throw new BadRequestException("Shift name already exists.");
            shift.ShiftName = name;
            shift.StartTime = request.StartTime;
            shift.EndTime = request.EndTime;
            shift.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return MapWorkShift(shift);
        }
        public async Task<bool> DeleteWorkShiftAsync(int workShiftId)
        {
            var shift = await _context.WorkShifts.FindAsync(workShiftId);
            if (shift == null) throw new NotFoundException("Work shift not found.");
            var hasAssignments = await _context.StaffShiftAssignments.AnyAsync(a => a.WorkShiftId == workShiftId);
            if (hasAssignments)
            {
                shift.IsActive = false;
            }
            else
            {
                _context.WorkShifts.Remove(shift);
            }
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<ShiftAssignmentResponseDTO>> GetShiftAssignmentsAsync(DateTime? fromDate, DateTime? toDate, int? staffUserId)
        {
            var query = BaseAssignmentQuery();
            ApplyAssignmentFilters(ref query, fromDate, toDate, staffUserId);
            var assignments = await query.OrderBy(a => a.WorkDate).ThenBy(a => a.WorkShift.StartTime).ToListAsync();
            return assignments.Select(MapAssignment).ToList();
        }
        public async Task<List<ShiftAssignmentResponseDTO>> GetMyShiftAssignmentsAsync(int staffUserId, DateTime? fromDate, DateTime? toDate)
        {
            var query = BaseAssignmentQuery();
            ApplyAssignmentFilters(ref query, fromDate, toDate, staffUserId);
            var assignments = await query.OrderBy(a => a.WorkDate).ThenBy(a => a.WorkShift.StartTime).ToListAsync();
            return assignments.Select(MapAssignment).ToList();
        }
        public async Task<List<ShiftAssignmentResponseDTO>> GetOtherStaffShiftAssignmentsAsync(int currentStaffUserId, DateTime? date, int? workShiftId)
        {
            var query = BaseAssignmentQuery().Where(a => a.StaffUserId != currentStaffUserId);
            if (date.HasValue) query = query.Where(a => a.WorkDate == date.Value.Date);
            if (workShiftId.HasValue) query = query.Where(a => a.WorkShiftId == workShiftId.Value);
            var assignments = await query.OrderBy(a => a.WorkDate).ThenBy(a => a.WorkShift.StartTime).ToListAsync();
            return assignments.Select(MapAssignment).ToList();
        }
        public async Task<ShiftAssignmentResponseDTO> CreateShiftAssignmentAsync(CreateShiftAssignmentDTO request)
        {
            var staff = await GetStaffUserAsync(request.StaffUserId);
            var shift = await _context.WorkShifts.FindAsync(request.WorkShiftId);
            if (shift == null || !shift.IsActive) throw new NotFoundException("Active work shift not found.");
            await EnsureNoAssignmentConflictAsync(request.StaffUserId, request.WorkShiftId, request.WorkDate.Date, null);
            var assignment = new StaffShiftAssignment
            {
                StaffUserId = staff.UserId,
                WorkShiftId = shift.WorkShiftId,
                WorkDate = request.WorkDate.Date,
                Status = "Scheduled",
                Note = request.Note?.Trim()
            };
            _context.StaffShiftAssignments.Add(assignment);
            await _context.SaveChangesAsync();
            return await GetAssignmentDtoAsync(assignment.AssignmentId);
        }
        public async Task<ShiftAssignmentResponseDTO> UpdateShiftAssignmentAsync(int assignmentId, UpdateShiftAssignmentDTO request)
        {
            var assignment = await _context.StaffShiftAssignments.FindAsync(assignmentId);
            if (assignment == null) throw new NotFoundException("Shift assignment not found.");
            var shift = await _context.WorkShifts.FindAsync(request.WorkShiftId);
            if (shift == null || !shift.IsActive) throw new NotFoundException("Active work shift not found.");
            await EnsureNoAssignmentConflictAsync(assignment.StaffUserId, request.WorkShiftId, request.WorkDate.Date, assignmentId);
            assignment.WorkShiftId = request.WorkShiftId;
            assignment.WorkDate = request.WorkDate.Date;
            assignment.Status = request.Status;
            assignment.Note = request.Note?.Trim();
            assignment.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            await _context.SaveChangesAsync();
            return await GetAssignmentDtoAsync(assignment.AssignmentId);
        }
        public async Task<bool> DeleteShiftAssignmentAsync(int assignmentId)
        {
            var assignment = await _context.StaffShiftAssignments.FindAsync(assignmentId);
            if (assignment == null) throw new NotFoundException("Shift assignment not found.");
            var hasPendingSwap = await _context.ShiftSwapRequests.AnyAsync(s =>
                s.Status == "Pending" && (s.FromAssignmentId == assignmentId || s.ToAssignmentId == assignmentId));
            if (hasPendingSwap)
                throw new BadRequestException("Cannot delete shift assignment with a pending swap request.");
            
            var relatedSwaps = await _context.ShiftSwapRequests.Where(s => 
                s.FromAssignmentId == assignmentId || s.ToAssignmentId == assignmentId).ToListAsync();
            _context.ShiftSwapRequests.RemoveRange(relatedSwaps);

            _context.StaffShiftAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<List<OvertimeRequestResponseDTO>> GetOvertimeRequestsAsync(string? status)
        {
            var query = BaseOvertimeQuery();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status.Trim());
            var requests = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            return requests.Select(MapOvertime).ToList();
        }
        public async Task<List<OvertimeRequestResponseDTO>> GetMyOvertimeRequestsAsync(int staffUserId)
        {
            var requests = await BaseOvertimeQuery()
                .Where(o => o.StaffUserId == staffUserId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return requests.Select(MapOvertime).ToList();
        }
        public async Task<OvertimeRequestResponseDTO> CreateOvertimeRequestAsync(int staffUserId, CreateOvertimeRequestDTO request)
        {
            ValidateTimeRange(request.StartTime, request.EndTime);
            await GetStaffUserAsync(staffUserId);
            var overtime = new OvertimeRequest
            {
                StaffUserId = staffUserId,
                WorkDate = request.WorkDate.Date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Reason = request.Reason?.Trim(),
                Status = "Pending"
            };
            _context.OvertimeRequests.Add(overtime);
            await _context.SaveChangesAsync();
            return await GetOvertimeDtoAsync(overtime.OvertimeRequestId);
        }
        public async Task<OvertimeRequestResponseDTO> ReviewOvertimeRequestAsync(int requestId, int managerUserId, ReviewRequestDTO request)
        {
            var overtime = await _context.OvertimeRequests.FindAsync(requestId);
            if (overtime == null) throw new NotFoundException("Overtime request not found.");
            if (overtime.Status != "Pending") throw new BadRequestException("This request has already been processed.");
            overtime.Status = request.IsApproved ? "Approved" : "Rejected";
            overtime.ReviewedByUserId = managerUserId;
            overtime.ReviewedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            overtime.ReviewNote = request.ReviewNote?.Trim();
            await _context.SaveChangesAsync();
            return await GetOvertimeDtoAsync(overtime.OvertimeRequestId);
        }
        public async Task<List<ShiftSwapRequestResponseDTO>> GetShiftSwapRequestsAsync(string? status)
        {
            var query = BaseSwapQuery();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status.Trim());
            var requests = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return requests.Select(MapSwap).ToList();
        }
        public async Task<List<ShiftSwapRequestResponseDTO>> GetMyShiftSwapRequestsAsync(int staffUserId)
        {
            var requests = await BaseSwapQuery()
                .Where(s => s.RequestedByUserId == staffUserId
                    || s.FromAssignment.StaffUserId == staffUserId
                    || (s.ToAssignment != null && s.ToAssignment.StaffUserId == staffUserId))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            return requests.Select(MapSwap).ToList();
        }
        public async Task<ShiftSwapRequestResponseDTO> CreateShiftSwapRequestAsync(int staffUserId, CreateShiftSwapRequestDTO request)
        {
            var from = await BaseAssignmentQuery().FirstOrDefaultAsync(a => a.AssignmentId == request.FromAssignmentId);
            if (from == null) throw new NotFoundException("Shift to swap not found.");
            if (from.StaffUserId != staffUserId) throw new BadRequestException("You can only submit swap requests from your own shift.");
            if (from.Status != "Scheduled") throw new BadRequestException("Can only swap shifts in Scheduled status.");

            var swap = new ShiftSwapRequest
            {
                FromAssignmentId = request.FromAssignmentId,
                RequestedByUserId = staffUserId,
                Reason = request.Reason?.Trim(),
                Status = "Pending"
            };

            if (request.ToAssignmentId.HasValue)
            {
                if (request.FromAssignmentId == request.ToAssignmentId.Value)
                    throw new BadRequestException("Cannot swap the same shift.");
                var to = await BaseAssignmentQuery().FirstOrDefaultAsync(a => a.AssignmentId == request.ToAssignmentId.Value);
                if (to == null) throw new NotFoundException("Target shift assignment not found.");
                if (from.WorkDate == to.WorkDate && from.WorkShiftId == to.WorkShiftId)
                    throw new BadRequestException("Cannot swap two assignments in the same shift on the same day.");
                if (to.Status != "Scheduled")
                    throw new BadRequestException("Can only swap shifts in Scheduled status.");

                var pendingExists = await _context.ShiftSwapRequests.AnyAsync(s =>
                    s.Status == "Pending" && (s.FromAssignmentId == request.FromAssignmentId || s.FromAssignmentId == request.ToAssignmentId.Value || s.ToAssignmentId == request.FromAssignmentId || s.ToAssignmentId == request.ToAssignmentId.Value));
                if (pendingExists) throw new BadRequestException("One of the shifts currently has a pending swap request.");

                swap.ToAssignmentId = request.ToAssignmentId.Value;
            }
            else
            {
                if (!request.ToWorkShiftId.HasValue || !request.ToWorkDate.HasValue)
                    throw new BadRequestException("Must provide either target assignment or target work shift and date.");
                
                var workShift = await _context.WorkShifts.FindAsync(request.ToWorkShiftId.Value);
                if (workShift == null || !workShift.IsActive) throw new NotFoundException("Active work shift not found.");
                if (from.WorkDate == request.ToWorkDate.Value.Date && from.WorkShiftId == request.ToWorkShiftId.Value)
                    throw new BadRequestException("Cannot swap to the same shift on the same day.");
                
                await EnsureNoAssignmentConflictAsync(staffUserId, request.ToWorkShiftId.Value, request.ToWorkDate.Value.Date, request.FromAssignmentId);

                var pendingExists = await _context.ShiftSwapRequests.AnyAsync(s =>
                    s.Status == "Pending" && (s.FromAssignmentId == request.FromAssignmentId || s.ToAssignmentId == request.FromAssignmentId));
                if (pendingExists) throw new BadRequestException("This shift currently has a pending swap request.");

                swap.ToWorkShiftId = request.ToWorkShiftId.Value;
                swap.ToWorkDate = request.ToWorkDate.Value.Date;
            }

            _context.ShiftSwapRequests.Add(swap);
            await _context.SaveChangesAsync();
            return await GetSwapDtoAsync(swap.ShiftSwapRequestId);
        }
        public async Task<ShiftSwapRequestResponseDTO> ReviewShiftSwapRequestAsync(int requestId, int managerUserId, ReviewRequestDTO request)
        {
            var swap = await _context.ShiftSwapRequests
                .Include(s => s.FromAssignment)
                .Include(s => s.ToAssignment)
                .FirstOrDefaultAsync(s => s.ShiftSwapRequestId == requestId);
            if (swap == null) throw new NotFoundException("Shift swap request not found.");
            if (swap.Status != "Pending") throw new BadRequestException("This request has already been processed.");
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                swap.Status = request.IsApproved ? "Approved" : "Rejected";
                swap.ReviewedByUserId = managerUserId;
                swap.ReviewedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                swap.ReviewNote = request.ReviewNote?.Trim();
                if (request.IsApproved)
                {
                    if (swap.ToAssignmentId.HasValue && swap.ToAssignment != null)
                    {
                        var fromStaffId = swap.FromAssignment.StaffUserId;
                        var toStaffId = swap.ToAssignment.StaffUserId;
                        await EnsureNoAssignmentConflictAsync(toStaffId, swap.FromAssignment.WorkShiftId, swap.FromAssignment.WorkDate, swap.ToAssignmentId);
                        await EnsureNoAssignmentConflictAsync(fromStaffId, swap.ToAssignment.WorkShiftId, swap.ToAssignment.WorkDate, swap.FromAssignmentId);
                        swap.FromAssignment.StaffUserId = swap.ToAssignment.StaffUserId;
                        swap.ToAssignment.StaffUserId = fromStaffId;
                        swap.FromAssignment.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                        swap.ToAssignment.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                    }
                    else if (swap.ToWorkShiftId.HasValue && swap.ToWorkDate.HasValue)
                    {
                        await EnsureNoAssignmentConflictAsync(swap.FromAssignment.StaffUserId, swap.ToWorkShiftId.Value, swap.ToWorkDate.Value, swap.FromAssignmentId);
                        swap.FromAssignment.WorkShiftId = swap.ToWorkShiftId.Value;
                        swap.FromAssignment.WorkDate = swap.ToWorkDate.Value;
                        swap.FromAssignment.UpdatedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                    }
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return await GetSwapDtoAsync(swap.ShiftSwapRequestId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private async Task<User> GetStaffUserAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.StaffProfile)
                .Include(u => u.ManagerProfile)
                .Include(u => u.EmployeeProfile).ThenInclude(e => e.Branch)
                .FirstOrDefaultAsync(u => u.UserId == userId && (u.Role == UserRoles.Staff || u.Role == UserRoles.Manager));
            if (user == null) throw new NotFoundException("Staff member not found.");
            return user;
        }
        private static void EnsurePersonnelRole(string role)
        {
            if (role != UserRoles.Staff && role != UserRoles.Manager)
                throw new BadRequestException("Role nhan su chi duoc phep la Staff hoac Manager.");
        }
        private async Task EnsureNoAssignmentConflictAsync(int staffUserId, int workShiftId, DateTime workDate, int? exceptAssignmentId)
        {
            var exists = await _context.StaffShiftAssignments.AnyAsync(a =>
                a.AssignmentId != exceptAssignmentId
                && a.StaffUserId == staffUserId
                && a.WorkShiftId == workShiftId
                && a.WorkDate == workDate.Date);
            if (exists) throw new BadRequestException("Employee is already assigned to this shift on the selected date.");
        }
        private static void ValidateTimeRange(TimeSpan start, TimeSpan end)
        {
            if (start >= end) throw new BadRequestException("Start time must be earlier than end time.");
        }
        private IQueryable<StaffShiftAssignment> BaseAssignmentQuery()
        {
            return _context.StaffShiftAssignments
                .Include(a => a.StaffUser).ThenInclude(u => u.StaffProfile)
                .Include(a => a.StaffUser).ThenInclude(u => u.ManagerProfile)
                .Include(a => a.WorkShift);
        }
        private static void ApplyAssignmentFilters(ref IQueryable<StaffShiftAssignment> query, DateTime? fromDate, DateTime? toDate, int? staffUserId)
        {
            if (fromDate.HasValue) query = query.Where(a => a.WorkDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(a => a.WorkDate <= toDate.Value.Date);
            if (staffUserId.HasValue) query = query.Where(a => a.StaffUserId == staffUserId.Value);
        }
        private IQueryable<OvertimeRequest> BaseOvertimeQuery()
        {
            return _context.OvertimeRequests
                .Include(o => o.StaffUser).ThenInclude(u => u.StaffProfile)
                .Include(o => o.StaffUser).ThenInclude(u => u.ManagerProfile);
        }
        private IQueryable<ShiftSwapRequest> BaseSwapQuery()
        {
            return _context.ShiftSwapRequests
                .Include(s => s.FromAssignment).ThenInclude(a => a.StaffUser).ThenInclude(u => u.StaffProfile)
                .Include(s => s.FromAssignment).ThenInclude(a => a.StaffUser).ThenInclude(u => u.ManagerProfile)
                .Include(s => s.FromAssignment).ThenInclude(a => a.WorkShift)
                .Include(s => s.ToAssignment).ThenInclude(a => a.StaffUser).ThenInclude(u => u.StaffProfile)
                .Include(s => s.ToAssignment).ThenInclude(a => a.StaffUser).ThenInclude(u => u.ManagerProfile)
                .Include(s => s.ToAssignment).ThenInclude(a => a.WorkShift)
                .Include(s => s.ToWorkShift);
        }
        private async Task<ShiftAssignmentResponseDTO> GetAssignmentDtoAsync(int assignmentId)
        {
            var assignment = await BaseAssignmentQuery().FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
            if (assignment == null) throw new NotFoundException("Shift assignment not found.");
            return MapAssignment(assignment);
        }
        private async Task<OvertimeRequestResponseDTO> GetOvertimeDtoAsync(int requestId)
        {
            var request = await BaseOvertimeQuery().FirstOrDefaultAsync(o => o.OvertimeRequestId == requestId);
            if (request == null) throw new NotFoundException("Overtime request not found.");
            return MapOvertime(request);
        }
        private async Task<ShiftSwapRequestResponseDTO> GetSwapDtoAsync(int requestId)
        {
            var request = await BaseSwapQuery().FirstOrDefaultAsync(s => s.ShiftSwapRequestId == requestId);
            if (request == null) throw new NotFoundException("Shift swap request not found.");
            return MapSwap(request);
        }
        private static StaffResponseDTO MapStaff(User user) => new()
        {
            UserId = user.UserId,
            FullName = user.Role == UserRoles.Manager
                ? user.ManagerProfile?.FullName ?? "N/A"
                : user.StaffProfile?.FullName ?? "N/A",
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status,
            Position = user.Role == UserRoles.Manager ? user.ManagerProfile?.Position : user.StaffProfile?.Position,
            HiredDate = user.Role == UserRoles.Manager ? user.ManagerProfile?.HiredDate : user.StaffProfile?.HiredDate,
            BranchId = user.EmployeeProfile?.BranchId,
            BranchName = user.EmployeeProfile?.Branch?.Name
        };
        private static WorkShiftResponseDTO MapWorkShift(WorkShift shift) => new()
        {
            WorkShiftId = shift.WorkShiftId,
            ShiftName = shift.ShiftName,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            IsActive = shift.IsActive
        };
        private static string GetPersonnelName(User user)
        {
            return user.Role == UserRoles.Manager
                ? user.ManagerProfile?.FullName ?? user.PhoneNumber
                : user.StaffProfile?.FullName ?? user.PhoneNumber;
        }
        private static ShiftAssignmentResponseDTO MapAssignment(StaffShiftAssignment assignment) => new()
        {
            AssignmentId = assignment.AssignmentId,
            StaffUserId = assignment.StaffUserId,
            StaffName = GetPersonnelName(assignment.StaffUser),
            WorkShiftId = assignment.WorkShiftId,
            ShiftName = assignment.WorkShift.ShiftName,
            WorkDate = assignment.WorkDate,
            StartTime = assignment.WorkShift.StartTime,
            EndTime = assignment.WorkShift.EndTime,
            Status = assignment.Status,
            Note = assignment.Note
        };
        private static OvertimeRequestResponseDTO MapOvertime(OvertimeRequest request) => new()
        {
            OvertimeRequestId = request.OvertimeRequestId,
            StaffUserId = request.StaffUserId,
            StaffName = GetPersonnelName(request.StaffUser),
            WorkDate = request.WorkDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Reason = request.Reason,
            Status = request.Status,
            ReviewNote = request.ReviewNote,
            CreatedAt = request.CreatedAt
        };
        private static ShiftSwapRequestResponseDTO MapSwap(ShiftSwapRequest request) => new()
        {
            ShiftSwapRequestId = request.ShiftSwapRequestId,
            FromAssignmentId = request.FromAssignmentId,
            ToAssignmentId = request.ToAssignmentId,
            ToWorkShiftId = request.ToWorkShiftId,
            RequestedByUserId = request.RequestedByUserId,
            RequestedByName = request.FromAssignment.StaffUserId == request.RequestedByUserId
                ? GetPersonnelName(request.FromAssignment.StaffUser)
                : (request.ToAssignment != null ? GetPersonnelName(request.ToAssignment.StaffUser) : "Unknown"),
            FromStaffName = GetPersonnelName(request.FromAssignment.StaffUser),
            ToStaffName = request.ToAssignment != null ? GetPersonnelName(request.ToAssignment.StaffUser) : null,
            FromWorkDate = request.FromAssignment.WorkDate,
            ToWorkDate = request.ToAssignment != null ? request.ToAssignment.WorkDate : request.ToWorkDate,
            Reason = request.Reason,
            Status = request.Status,
            ReviewNote = request.ReviewNote,
            CreatedAt = request.CreatedAt
        };
    }
}
#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
