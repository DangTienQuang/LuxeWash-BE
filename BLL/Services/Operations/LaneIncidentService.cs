using AutoWashPro.BLL.DTOs.Operations;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services.Operations
{
    public class LaneIncidentService : ILaneIncidentService
    {
        private static readonly HashSet<string> ValidIssueTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "broken", "maintenance", "power", "water", "other"
        };

        private static readonly HashSet<string> OpenIncidentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "pending", "in_progress"
        };

        private readonly AutoWashDbContext _context;
        private readonly IUserNotificationService _notificationService;
        private readonly ILaneDisplayPublisherService _laneDisplayPublisher;

        public LaneIncidentService(
            AutoWashDbContext context,
            IUserNotificationService notificationService,
            ILaneDisplayPublisherService laneDisplayPublisher)
        {
            _context = context;
            _notificationService = notificationService;
            _laneDisplayPublisher = laneDisplayPublisher;
        }

        public async Task<List<ManagerLaneStatusDTO>> GetManagerLaneStatusesAsync(int managerUserId)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            return await GetLaneStatusesForBranchAsync(branchId);
        }

        public async Task<List<LaneIncidentResponseDTO>> GetManagerIncidentsAsync(int managerUserId, string? status = null)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            ValidateStatusFilter(status);

            var query = _context.LaneIncidents
                .AsNoTracking()
                .Include(i => i.Lane)
                .Where(i => i.BranchId == branchId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }

            var incidents = await query
                .OrderByDescending(i => i.ReportedAt)
                .ToListAsync();

            return incidents.Select(ToIncidentResponse).ToList();
        }

        public async Task<LaneIncidentDetailDTO> GetManagerIncidentAsync(int managerUserId, int incidentId)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var incident = await _context.LaneIncidents
                .AsNoTracking()
                .Include(i => i.Lane)
                .Include(i => i.Dispatches)
                    .ThenInclude(d => d.Lane)
                .FirstOrDefaultAsync(i => i.Id == incidentId && i.BranchId == branchId);

            if (incident == null)
            {
                throw new NotFoundException("Incident not found in your branch.");
            }

            var response = ToIncidentDetail(incident);
            response.Dispatches = incident.Dispatches
                .OrderByDescending(d => d.DispatchedAt)
                .Select(StaffLaneDispatchService.ToDispatchResponse)
                .ToList();

            return response;
        }

        public async Task<LaneIncidentResponseDTO> ResolveIncidentAsync(
            int managerUserId,
            int incidentId,
            ResolveLaneIncidentDTO request)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var incident = await _context.LaneIncidents
                .Include(i => i.Lane)
                .FirstOrDefaultAsync(i => i.Id == incidentId && i.BranchId == branchId);

            if (incident == null)
            {
                throw new NotFoundException("Incident not found in your branch.");
            }

            if (incident.Status == "resolved")
            {
                throw new BadRequestException("Incident is already resolved.");
            }

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            incident.Status = "resolved";
            incident.ResolvedAt = now;
            incident.ResolvedByUserId = managerUserId;
            incident.ResolutionNote = request.ResolutionNote;
            incident.LaneReactivated = request.Reactivated;

            if (request.Reactivated)
            {
                incident.Lane.IsActive = true;
                incident.Lane.DeactivationReason = null;
                incident.Lane.DeactivatedByUserId = null;
                incident.Lane.DeactivatedAt = null;
            }

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                incident.ReportedByUserId,
                "Sự cố đã giải quyết",
                $"{incident.Lane.Name} đã được xử lý.",
                "LaneIncidentResolved",
                incident.Id.ToString());

            await _laneDisplayPublisher.PublishLaneStatusChangedAsync(
                incident.BranchId,
                incident.LaneId,
                incident.Lane.Name,
                incident.Lane.IsActive,
                request.Reactivated ? "resolved" : incident.Lane.DeactivationReason);

            return ToIncidentResponse(incident);
        }

        public async Task DeleteIncidentAsync(int managerUserId, int incidentId)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var incident = await _context.LaneIncidents
                .FirstOrDefaultAsync(i => i.Id == incidentId && i.BranchId == branchId);

            if (incident == null)
            {
                throw new NotFoundException("Incident not found in your branch.");
            }

            if (incident.Status != "resolved")
            {
                throw new BadRequestException("Only resolved incidents can be deleted.");
            }

            _context.LaneIncidents.Remove(incident);
            await _context.SaveChangesAsync();
        }

        public async Task<ManagerLaneStatusDTO> ReactivateLaneAsync(
            int managerUserId,
            int laneId,
            ReactivateLaneDTO request)
        {
            var branchId = await GetRequiredBranchIdAsync(managerUserId, "Manager");
            var lane = await _context.Lanes
                .FirstOrDefaultAsync(l => l.LaneId == laneId && l.BranchId == branchId);

            if (lane == null)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            var openIncidents = await _context.LaneIncidents
                .Where(i => i.LaneId == laneId && OpenIncidentStatuses.Contains(i.Status))
                .ToListAsync();

            var now = AutoWashPro.DAL.Helpers.TimeHelper.VnNow;
            foreach (var incident in openIncidents)
            {
                incident.Status = "resolved";
                incident.ResolvedAt = now;
                incident.ResolvedByUserId = managerUserId;
                incident.ResolutionNote = request.ResolutionNote;
                incident.LaneReactivated = true;
            }

            lane.IsActive = true;
            lane.DeactivationReason = null;
            lane.DeactivatedByUserId = null;
            lane.DeactivatedAt = null;

            await _context.SaveChangesAsync();
            await _laneDisplayPublisher.PublishLaneStatusChangedAsync(branchId, lane.LaneId, lane.Name, true, "manual_reactivate");

            return (await GetLaneStatusesForBranchAsync(branchId)).First(l => l.LaneId == laneId);
        }

        public async Task<LaneIncidentResponseDTO> CreateStaffIncidentAsync(int staffUserId, CreateLaneIncidentDTO request)
        {
            ValidateIssueType(request.IssueType);
            var branchId = await GetRequiredBranchIdAsync(staffUserId, "Staff");
            var reporterName = await GetEmployeeFullNameAsync(staffUserId);
            var lane = await _context.Lanes
                .FirstOrDefaultAsync(l => l.LaneId == request.LaneId && l.BranchId == branchId);

            if (lane == null)
            {
                throw new NotFoundException("Lane not found in your branch.");
            }

            var incident = new LaneIncident
            {
                LaneId = lane.LaneId,
                BranchId = branchId,
                ReportedByUserId = staffUserId,
                ReportedByFullName = reporterName,
                IssueType = Normalize(request.IssueType),
                Description = request.Description.Trim(),
                ReportedAt = AutoWashPro.DAL.Helpers.TimeHelper.VnNow,
                Status = "pending"
            };

            lane.IsActive = false;
            lane.DeactivationReason = $"incident: {incident.IssueType}";
            lane.DeactivatedByUserId = staffUserId;
            lane.DeactivatedAt = incident.ReportedAt;

            _context.LaneIncidents.Add(incident);
            await _context.SaveChangesAsync();

            var managerIds = await _context.EmployeeProfiles
                .Include(e => e.User)
                .Where(e => e.BranchId == branchId && e.User.Role == "Manager" && e.User.Status == "Active")
                .Select(e => e.EmployeeId)
                .ToListAsync();

            await _notificationService.CreateNotificationsBulkAsync(
                managerIds,
                "Sự cố làn rửa mới",
                $"{lane.Name} báo lỗi: {incident.IssueType}.",
                "LaneIncidentCreated",
                incident.Id.ToString());

            await _laneDisplayPublisher.PublishLaneStatusChangedAsync(
                branchId,
                lane.LaneId,
                lane.Name,
                false,
                lane.DeactivationReason);

            return ToIncidentResponse(incident, lane.Name);
        }

        public async Task<List<LaneIncidentResponseDTO>> GetStaffReportsAsync(int staffUserId)
        {
            var branchId = await GetRequiredBranchIdAsync(staffUserId, "Staff");
            var incidents = await _context.LaneIncidents
                .AsNoTracking()
                .Include(i => i.Lane)
                .Where(i => i.BranchId == branchId && i.ReportedByUserId == staffUserId)
                .OrderByDescending(i => i.ReportedAt)
                .ToListAsync();

            return incidents.Select(ToIncidentResponse).ToList();
        }

        public async Task<List<ManagerLaneStatusDTO>> GetStaffLanesAsync(int staffUserId)
        {
            var branchId = await GetRequiredBranchIdAsync(staffUserId, "Staff");
            return await GetLaneStatusesForBranchAsync(branchId);
        }

        private async Task<List<ManagerLaneStatusDTO>> GetLaneStatusesForBranchAsync(int branchId)
        {
            var lanes = await _context.Lanes
                .AsNoTracking()
                .Where(l => l.BranchId == branchId)
                .OrderBy(l => l.LaneId)
                .ToListAsync();

            var laneIds = lanes.Select(l => l.LaneId).ToList();
            var openIncidents = await _context.LaneIncidents
                .AsNoTracking()
                .Include(i => i.Lane)
                .Where(i => laneIds.Contains(i.LaneId) && OpenIncidentStatuses.Contains(i.Status))
                .OrderByDescending(i => i.ReportedAt)
                .ToListAsync();

            var latestIncidentByLane = openIncidents
                .GroupBy(i => i.LaneId)
                .ToDictionary(g => g.Key, g => g.First());

            return lanes.Select(l =>
            {
                latestIncidentByLane.TryGetValue(l.LaneId, out var incident);
                return new ManagerLaneStatusDTO
                {
                    LaneId = l.LaneId,
                    LaneName = l.Name,
                    BranchId = l.BranchId,
                    IsActive = l.IsActive,
                    IsBusinessLane = l.IsBusinessLane,
                    IsVipLane = l.IsVipLane,
                    HasOpenIncident = incident != null,
                    DeactivationReason = l.DeactivationReason,
                    DeactivatedByUserId = l.DeactivatedByUserId,
                    DeactivatedAt = l.DeactivatedAt,
                    ActiveIncident = incident == null ? null : ToIncidentResponse(incident)
                };
            }).ToList();
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

        private static LaneIncidentResponseDTO ToIncidentResponse(LaneIncident incident)
        {
            return ToIncidentResponse(incident, incident.Lane.Name);
        }

        private static LaneIncidentResponseDTO ToIncidentResponse(LaneIncident incident, string laneName)
        {
            return new LaneIncidentResponseDTO
            {
                IncidentId = incident.Id,
                LaneId = incident.LaneId,
                LaneName = laneName,
                BranchId = incident.BranchId,
                ReportedByUserId = incident.ReportedByUserId,
                ReportedByFullName = incident.ReportedByFullName,
                IssueType = incident.IssueType,
                Description = incident.Description,
                ReportedAt = incident.ReportedAt,
                Status = incident.Status,
                ResolvedAt = incident.ResolvedAt,
                ResolvedByUserId = incident.ResolvedByUserId,
                ResolutionNote = incident.ResolutionNote,
                LaneReactivated = incident.LaneReactivated
            };
        }

        private static LaneIncidentDetailDTO ToIncidentDetail(LaneIncident incident)
        {
            var response = ToIncidentResponse(incident);
            return new LaneIncidentDetailDTO
            {
                IncidentId = response.IncidentId,
                LaneId = response.LaneId,
                LaneName = response.LaneName,
                BranchId = response.BranchId,
                ReportedByUserId = response.ReportedByUserId,
                ReportedByFullName = response.ReportedByFullName,
                IssueType = response.IssueType,
                Description = response.Description,
                ReportedAt = response.ReportedAt,
                Status = response.Status,
                ResolvedAt = response.ResolvedAt,
                ResolvedByUserId = response.ResolvedByUserId,
                ResolutionNote = response.ResolutionNote,
                LaneReactivated = response.LaneReactivated
            };
        }

        private static void ValidateIssueType(string issueType)
        {
            if (!ValidIssueTypes.Contains(issueType))
            {
                throw new BadRequestException("Invalid issue type.");
            }
        }

        private static void ValidateStatusFilter(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            if (status != "pending" && status != "in_progress" && status != "resolved")
            {
                throw new BadRequestException("Invalid incident status.");
            }
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }
    }
}
