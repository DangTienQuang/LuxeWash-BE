using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services.Operations
{
    public class StaffLaneDispatchService : IStaffLaneDispatchService
    {
        private static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
        {
            "incident", "maintenance", "inspection"
        };

        private static readonly HashSet<string> ValidResolutions = new(StringComparer.OrdinalIgnoreCase)
        {
            "fixed", "needs_manager", "escalated"
        };

        private readonly AutoWashDbContext _context;
        private readonly IUserNotificationService _notificationService;

        public StaffLaneDispatchService(
            AutoWashDbContext context,
            IUserNotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<StaffLaneDispatchResponseDTO> CreateDispatchAsync(
            int managerUserId,
            CreateStaffLaneDispatchDTO request)
        {
            ValidateReason(request.Reason);
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var managerName = await GetEmployeeFullNameAsync(managerUserId);

            var lane = await _context.Lanes
                .FirstOrDefaultAsync(l => l.LaneId == request.LaneId && l.BranchId == branchId);
            if (lane == null)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            var staff = await _context.EmployeeProfiles
                .Include(e => e.User)
                .FirstOrDefaultAsync(e =>
                    e.EmployeeId == request.StaffUserId &&
                    e.BranchId == branchId &&
                    e.User.Role == "Staff");
            if (staff == null)
            {
                throw new NotFoundException("Staff not found in your branch.");
            }

            LaneIncident? incident = null;
            if (request.RelatedIncidentId.HasValue)
            {
                incident = await _context.LaneIncidents
                    .FirstOrDefaultAsync(i =>
                        i.Id == request.RelatedIncidentId.Value &&
                        i.BranchId == branchId &&
                        i.LaneId == lane.LaneId);
                if (incident == null)
                {
                    throw new NotFoundException("Related incident not found for this lane.");
                }

                if (incident.Status == "resolved")
                {
                    throw new BadRequestException("Cannot dispatch staff for a resolved incident.");
                }

                incident.Status = "in_progress";
            }

            var dispatch = new StaffLaneDispatch
            {
                LaneId = lane.LaneId,
                BranchId = branchId,
                DispatchedByUserId = managerUserId,
                DispatchedByFullName = managerName,
                StaffUserId = staff.EmployeeId,
                StaffFullName = staff.FullName,
                Reason = Normalize(request.Reason),
                Note = request.Note?.Trim(),
                DispatchedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                Status = "pending",
                RelatedIncidentId = incident?.Id
            };

            _context.StaffLaneDispatches.Add(dispatch);
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                dispatch.StaffUserId,
                "Điều động kiểm tra làn",
                $"Bạn được điều động kiểm tra {lane.Name}" +
                    (string.IsNullOrWhiteSpace(dispatch.Note) ? "." : $" - {dispatch.Note}"),
                "StaffLaneDispatchCreated",
                dispatch.Id.ToString());

            return ToDispatchResponse(dispatch, lane.Name);
        }

        public async Task<List<StaffLaneDispatchResponseDTO>> GetManagerDispatchesAsync(
            int managerUserId,
            string? status = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            ValidateStatusFilter(status);

            var query = _context.StaffLaneDispatches
                .AsNoTracking()
                .Include(d => d.Lane)
                .Where(d => d.BranchId == branchId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.Status == status);
            }

            if (from.HasValue)
            {
                query = query.Where(d => d.DispatchedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(d => d.DispatchedAt <= to.Value);
            }

            var dispatches = await query
                .OrderByDescending(d => d.DispatchedAt)
                .ToListAsync();

            return dispatches.Select(ToDispatchResponse).ToList();
        }

        public async Task CancelDispatchAsync(int managerUserId, int dispatchId)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var dispatch = await _context.StaffLaneDispatches
                .FirstOrDefaultAsync(d => d.Id == dispatchId && d.BranchId == branchId);

            if (dispatch == null)
            {
                throw new NotFoundException("Dispatch not found in your branch.");
            }

            if (dispatch.Status != "pending")
            {
                throw new BadRequestException("Only pending dispatches can be cancelled.");
            }

            dispatch.Status = "cancelled";
            await _context.SaveChangesAsync();
        }

        public async Task<List<StaffLaneDispatchResponseDTO>> GetActiveDispatchesForStaffAsync(
            int managerUserId,
            int staffUserId)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var staffExists = await _context.EmployeeProfiles
                .Include(e => e.User)
                .AnyAsync(e => e.EmployeeId == staffUserId && e.BranchId == branchId && e.User.Role == "Staff");

            if (!staffExists)
            {
                throw new NotFoundException("Staff not found in your branch.");
            }

            var dispatches = await _context.StaffLaneDispatches
                .AsNoTracking()
                .Include(d => d.Lane)
                .Where(d =>
                    d.BranchId == branchId &&
                    d.StaffUserId == staffUserId &&
                    d.Status != "completed" &&
                    d.Status != "cancelled")
                .OrderByDescending(d => d.DispatchedAt)
                .ToListAsync();

            return dispatches.Select(ToDispatchResponse).ToList();
        }

        public async Task<List<StaffLaneDispatchResponseDTO>> GetStaffDispatchesAsync(
            int staffUserId,
            string? status = null)
        {
            await GetRequiredBranchIdAsync(staffUserId, "Staff");
            ValidateStatusFilter(status);

            var query = _context.StaffLaneDispatches
                .AsNoTracking()
                .Include(d => d.Lane)
                .Where(d => d.StaffUserId == staffUserId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.Status == status);
            }

            var dispatches = await query
                .OrderByDescending(d => d.DispatchedAt)
                .ToListAsync();

            return dispatches.Select(ToDispatchResponse).ToList();
        }

        public async Task<StaffLaneDispatchResponseDTO> AcknowledgeDispatchAsync(int staffUserId, int dispatchId)
        {
            var branchId = await GetRequiredBranchIdAsync(staffUserId, "Staff");
            var dispatch = await _context.StaffLaneDispatches
                .Include(d => d.Lane)
                .FirstOrDefaultAsync(d =>
                    d.Id == dispatchId &&
                    d.StaffUserId == staffUserId &&
                    d.BranchId == branchId);

            if (dispatch == null)
            {
                throw new NotFoundException("Dispatch not found.");
            }

            if (dispatch.Status == "cancelled")
            {
                throw new BadRequestException("Dispatch was cancelled.");
            }

            if (dispatch.Status == "completed")
            {
                throw new BadRequestException("Dispatch is already completed.");
            }

            if (dispatch.Status == "pending")
            {
                dispatch.Status = "acknowledged";
                dispatch.AcknowledgedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
                await _context.SaveChangesAsync();
            }

            return ToDispatchResponse(dispatch);
        }

        public async Task<StaffLaneDispatchResponseDTO> CompleteDispatchAsync(
            int staffUserId,
            int dispatchId,
            CompleteStaffLaneDispatchDTO request)
        {
            ValidateResolution(request.IncidentResolution);
            var branchId = await GetRequiredBranchIdAsync(staffUserId, "Staff");
            var dispatch = await _context.StaffLaneDispatches
                .Include(d => d.Lane)
                .Include(d => d.RelatedIncident)
                .FirstOrDefaultAsync(d =>
                    d.Id == dispatchId &&
                    d.StaffUserId == staffUserId &&
                    d.BranchId == branchId);

            if (dispatch == null)
            {
                throw new NotFoundException("Dispatch not found.");
            }

            if (dispatch.Status == "cancelled")
            {
                throw new BadRequestException("Dispatch was cancelled.");
            }

            if (dispatch.Status == "completed")
            {
                throw new BadRequestException("Dispatch is already completed.");
            }

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            dispatch.Status = "completed";
            dispatch.CompletedAt = now;
            dispatch.AcknowledgedAt ??= now;
            dispatch.NoteFromStaff = request.NoteFromStaff?.Trim();
            dispatch.IncidentResolution = Normalize(request.IncidentResolution);

            if (dispatch.RelatedIncident != null && dispatch.RelatedIncident.Status == "pending")
            {
                dispatch.RelatedIncident.Status = "in_progress";
            }

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                dispatch.DispatchedByUserId,
                "Điều động hoàn thành",
                $"{dispatch.StaffFullName} đã hoàn thành kiểm tra {dispatch.Lane.Name}.",
                "StaffLaneDispatchCompleted",
                dispatch.Id.ToString());

            return ToDispatchResponse(dispatch);
        }

        public static StaffLaneDispatchResponseDTO ToDispatchResponse(StaffLaneDispatch dispatch)
        {
            return ToDispatchResponse(dispatch, dispatch.Lane.Name);
        }

        public static StaffLaneDispatchResponseDTO ToDispatchResponse(StaffLaneDispatch dispatch, string laneName)
        {
            return new StaffLaneDispatchResponseDTO
            {
                DispatchId = dispatch.Id,
                LaneId = dispatch.LaneId,
                LaneName = laneName,
                BranchId = dispatch.BranchId,
                DispatchedByUserId = dispatch.DispatchedByUserId,
                DispatchedByFullName = dispatch.DispatchedByFullName,
                StaffUserId = dispatch.StaffUserId,
                StaffFullName = dispatch.StaffFullName,
                Reason = dispatch.Reason,
                Note = dispatch.Note,
                DispatchedAt = dispatch.DispatchedAt,
                Status = dispatch.Status,
                AcknowledgedAt = dispatch.AcknowledgedAt,
                CompletedAt = dispatch.CompletedAt,
                NoteFromStaff = dispatch.NoteFromStaff,
                IncidentResolution = dispatch.IncidentResolution,
                RelatedIncidentId = dispatch.RelatedIncidentId
            };
        }

        private async Task<int> GetRequiredBranchIdAsync(int userId, string role)
        {
            var profile = await _context.EmployeeProfiles
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.EmployeeId == userId && e.User.Role == role);

            if (profile == null)
            {
                throw new BadRequestException($"{role} profile not found.");
            }

            if (!profile.BranchId.HasValue)
            {
                throw new BadRequestException($"{role} is not assigned to any branch.");
            }

            return profile.BranchId.Value;
        }

        private async Task<string> GetEmployeeFullNameAsync(int userId)
        {
            return await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == userId)
                .Select(e => e.FullName)
                .FirstOrDefaultAsync()
                ?? "Unknown";
        }

        private static void ValidateReason(string reason)
        {
            if (!ValidReasons.Contains(reason))
            {
                throw new BadRequestException("Invalid dispatch reason.");
            }
        }

        private static void ValidateResolution(string resolution)
        {
            if (!ValidResolutions.Contains(resolution))
            {
                throw new BadRequestException("Invalid incident resolution.");
            }
        }

        private static void ValidateStatusFilter(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            if (status != "pending" && status != "acknowledged" && status != "completed" && status != "cancelled")
            {
                throw new BadRequestException("Invalid dispatch status.");
            }
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }
    }
}
