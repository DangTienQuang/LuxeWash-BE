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
            if (user.Role != role) throw new NotFoundException("Khong tim thay nhan su.");
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
            if (exists) throw new BadRequestException("Số điện thoại này đã được đăng ký.");

            var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();
            if (email != null && await _context.Users.AnyAsync(u => u.Email == email))
                throw new BadRequestException("Email này đã được sử dụng.");

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
                    HiredDate = request.HiredDate?.Date ?? DateTime.UtcNow.Date
                };
            }
            else
            {
                user.StaffProfile = new StaffProfile
                {
                    FullName = request.FullName.Trim(),
                    Position = request.Position?.Trim(),
                    HiredDate = request.HiredDate?.Date ?? DateTime.UtcNow.Date
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
                    throw new BadRequestException("Số điện thoại này đã được sử dụng.");
                user.PhoneNumber = phone;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var email = request.Email.Trim().ToLower();
                if (await _context.Users.AnyAsync(u => u.UserId != staffUserId && u.Email == email))
                    throw new BadRequestException("Email này đã được sử dụng.");
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
            if (user.Role != role) throw new NotFoundException("Khong tim thay nhan su.");
            return await UpdateStaffAsync(staffUserId, request);
        }

        public async Task<bool> UpdateStaffStatusAsync(int staffUserId, string status)
        {
            if (status != UserStatuses.Active && status != UserStatuses.Blocked)
                throw new BadRequestException("Trạng thái chỉ được phép là Active hoặc Blocked.");

            var user = await GetStaffUserAsync(staffUserId);
            user.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SoftDeleteStaffByRoleAsync(int staffUserId, string role)
        {
            EnsurePersonnelRole(role);
            var user = await GetStaffUserAsync(staffUserId);
            if (user.Role != role) throw new NotFoundException("Khong tim thay nhan su.");

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
                throw new BadRequestException("Tên ca đã tồn tại.");

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
            if (shift == null) throw new NotFoundException("Không tìm thấy ca làm việc.");

            var name = request.ShiftName.Trim();
            if (await _context.WorkShifts.AnyAsync(s => s.WorkShiftId != workShiftId && s.ShiftName.ToLower() == name.ToLower()))
                throw new BadRequestException("Tên ca đã tồn tại.");

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
            if (shift == null) throw new NotFoundException("Không tìm thấy ca làm việc.");

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

        public async Task<ShiftAssignmentResponseDTO> CreateShiftAssignmentAsync(CreateShiftAssignmentDTO request)
        {
            var staff = await GetStaffUserAsync(request.StaffUserId);
            var shift = await _context.WorkShifts.FindAsync(request.WorkShiftId);
            if (shift == null || !shift.IsActive) throw new NotFoundException("Không tìm thấy ca làm việc đang hoạt động.");

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
            if (assignment == null) throw new NotFoundException("Không tìm thấy phân công ca.");

            var shift = await _context.WorkShifts.FindAsync(request.WorkShiftId);
            if (shift == null || !shift.IsActive) throw new NotFoundException("Không tìm thấy ca làm việc đang hoạt động.");

            await EnsureNoAssignmentConflictAsync(assignment.StaffUserId, request.WorkShiftId, request.WorkDate.Date, assignmentId);

            assignment.WorkShiftId = request.WorkShiftId;
            assignment.WorkDate = request.WorkDate.Date;
            assignment.Status = request.Status;
            assignment.Note = request.Note?.Trim();
            assignment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetAssignmentDtoAsync(assignment.AssignmentId);
        }

        public async Task<bool> DeleteShiftAssignmentAsync(int assignmentId)
        {
            var assignment = await _context.StaffShiftAssignments.FindAsync(assignmentId);
            if (assignment == null) throw new NotFoundException("Không tìm thấy phân công ca.");

            var hasPendingSwap = await _context.ShiftSwapRequests.AnyAsync(s =>
                s.Status == "Pending" && (s.FromAssignmentId == assignmentId || s.ToAssignmentId == assignmentId));
            if (hasPendingSwap)
                throw new BadRequestException("Không thể xóa phân công đang có yêu cầu đổi ca chờ duyệt.");

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
            if (overtime == null) throw new NotFoundException("Không tìm thấy yêu cầu tăng ca.");
            if (overtime.Status != "Pending") throw new BadRequestException("Yêu cầu này đã được xử lý.");

            overtime.Status = request.IsApproved ? "Approved" : "Rejected";
            overtime.ReviewedByUserId = managerUserId;
            overtime.ReviewedAt = DateTime.UtcNow;
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
                    || s.ToAssignment.StaffUserId == staffUserId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            return requests.Select(MapSwap).ToList();
        }

        public async Task<ShiftSwapRequestResponseDTO> CreateShiftSwapRequestAsync(int staffUserId, CreateShiftSwapRequestDTO request)
        {
            if (request.FromAssignmentId == request.ToAssignmentId)
                throw new BadRequestException("Không thể đổi cùng một ca.");

            var from = await BaseAssignmentQuery().FirstOrDefaultAsync(a => a.AssignmentId == request.FromAssignmentId);
            var to = await BaseAssignmentQuery().FirstOrDefaultAsync(a => a.AssignmentId == request.ToAssignmentId);
            if (from == null || to == null) throw new NotFoundException("Không tìm thấy ca cần đổi.");
            if (from.StaffUserId != staffUserId) throw new BadRequestException("Bạn chỉ có thể gửi yêu cầu đổi từ ca của chính mình.");
            if (from.WorkDate == to.WorkDate && from.WorkShiftId == to.WorkShiftId)
                throw new BadRequestException("Không thể đổi hai phân công trong cùng một ca và cùng một ngày.");
            if (from.Status != "Scheduled" || to.Status != "Scheduled")
                throw new BadRequestException("Chỉ có thể đổi các ca đang ở trạng thái Scheduled.");

            var pendingExists = await _context.ShiftSwapRequests.AnyAsync(s =>
                s.Status == "Pending" && (s.FromAssignmentId == request.FromAssignmentId || s.ToAssignmentId == request.ToAssignmentId));
            if (pendingExists) throw new BadRequestException("Một trong hai ca đang có yêu cầu đổi ca chờ duyệt.");

            var swap = new ShiftSwapRequest
            {
                FromAssignmentId = request.FromAssignmentId,
                ToAssignmentId = request.ToAssignmentId,
                RequestedByUserId = staffUserId,
                Reason = request.Reason?.Trim(),
                Status = "Pending"
            };

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

            if (swap == null) throw new NotFoundException("Không tìm thấy yêu cầu đổi ca.");
            if (swap.Status != "Pending") throw new BadRequestException("Yêu cầu này đã được xử lý.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                swap.Status = request.IsApproved ? "Approved" : "Rejected";
                swap.ReviewedByUserId = managerUserId;
                swap.ReviewedAt = DateTime.UtcNow;
                swap.ReviewNote = request.ReviewNote?.Trim();

                if (request.IsApproved)
                {
                    var fromStaffId = swap.FromAssignment.StaffUserId;
                    var toStaffId = swap.ToAssignment.StaffUserId;

                    await EnsureNoAssignmentConflictAsync(toStaffId, swap.FromAssignment.WorkShiftId, swap.FromAssignment.WorkDate, swap.ToAssignmentId);
                    await EnsureNoAssignmentConflictAsync(fromStaffId, swap.ToAssignment.WorkShiftId, swap.ToAssignment.WorkDate, swap.FromAssignmentId);

                    swap.FromAssignment.StaffUserId = swap.ToAssignment.StaffUserId;
                    swap.ToAssignment.StaffUserId = fromStaffId;
                    swap.FromAssignment.UpdatedAt = DateTime.UtcNow;
                    swap.ToAssignment.UpdatedAt = DateTime.UtcNow;
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

            if (user == null) throw new NotFoundException("Không tìm thấy nhân sự.");
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

            if (exists) throw new BadRequestException("Nhân sự đã được phân công ca này trong ngày đã chọn.");
        }

        private static void ValidateTimeRange(TimeSpan start, TimeSpan end)
        {
            if (start >= end) throw new BadRequestException("Thời gian bắt đầu phải nhỏ hơn thời gian kết thúc.");
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
                .Include(s => s.ToAssignment).ThenInclude(a => a.WorkShift);
        }

        private async Task<ShiftAssignmentResponseDTO> GetAssignmentDtoAsync(int assignmentId)
        {
            var assignment = await BaseAssignmentQuery().FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
            if (assignment == null) throw new NotFoundException("Không tìm thấy phân công ca.");
            return MapAssignment(assignment);
        }

        private async Task<OvertimeRequestResponseDTO> GetOvertimeDtoAsync(int requestId)
        {
            var request = await BaseOvertimeQuery().FirstOrDefaultAsync(o => o.OvertimeRequestId == requestId);
            if (request == null) throw new NotFoundException("Không tìm thấy yêu cầu tăng ca.");
            return MapOvertime(request);
        }

        private async Task<ShiftSwapRequestResponseDTO> GetSwapDtoAsync(int requestId)
        {
            var request = await BaseSwapQuery().FirstOrDefaultAsync(s => s.ShiftSwapRequestId == requestId);
            if (request == null) throw new NotFoundException("Không tìm thấy yêu cầu đổi ca.");
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
            RequestedByUserId = request.RequestedByUserId,
            RequestedByName = request.FromAssignment.StaffUserId == request.RequestedByUserId
                ? GetPersonnelName(request.FromAssignment.StaffUser)
                : GetPersonnelName(request.ToAssignment.StaffUser),
            FromStaffName = GetPersonnelName(request.FromAssignment.StaffUser),
            ToStaffName = GetPersonnelName(request.ToAssignment.StaffUser),
            FromWorkDate = request.FromAssignment.WorkDate,
            ToWorkDate = request.ToAssignment.WorkDate,
            Reason = request.Reason,
            Status = request.Status,
            ReviewNote = request.ReviewNote,
            CreatedAt = request.CreatedAt
        };
    }
}
