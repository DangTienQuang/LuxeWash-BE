using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;

using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class LaneService : ILaneService
    {
        private readonly AutoWashDbContext _context;

        public LaneService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<List<LaneDTO>> GetAllLanesAsync(int? branchId = null)
        {
            var query = _context.Lanes.AsQueryable();

            if (branchId.HasValue)
            {
                query = query.Where(l => l.BranchId == branchId.Value);
            }

            var lanes = await query.ToListAsync();
            return lanes.Select(l => new LaneDTO
            {
                LaneId = l.LaneId,
                Name = l.Name,
                BranchId = l.BranchId,
                IsActive = l.IsActive,
                IsBusinessLane = l.IsBusinessLane,
            }).ToList();
        }

        public async Task<LaneDTO> GetLaneByIdAsync(int laneId)
        {
            var lane = await _context.Lanes.FindAsync(laneId);
            if (lane == null) throw new NotFoundException("Lane not found.");

            return new LaneDTO
            {
                LaneId = lane.LaneId,
                Name = lane.Name,
                BranchId = lane.BranchId,
                IsActive = lane.IsActive,
                IsBusinessLane = lane.IsBusinessLane
            };
        }

        public async Task<LaneDTO> CreateLaneAsync(CreateLaneDTO createDto)
        {
            var branch = await _context.Branches.FindAsync(createDto.BranchId);
            if (branch == null) throw new NotFoundException("Branch not found.");

            var lane = new Lane
            {
                Name = createDto.Name,
                BranchId = createDto.BranchId,
                IsActive = true,
                IsBusinessLane = false
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

        public async Task<LaneDTO> UpdateLaneAsync(int laneId, UpdateLaneDTO updateDto)
        {
            var lane = await _context.Lanes.FindAsync(laneId);
            if (lane == null) throw new NotFoundException("Lane not found.");

            if (lane.BranchId != updateDto.BranchId)
            {
                var branch = await _context.Branches.FindAsync(updateDto.BranchId);
                if (branch == null) throw new NotFoundException("Branch not found.");
            }

            lane.Name = updateDto.Name;
            lane.BranchId = updateDto.BranchId;
            lane.IsActive = updateDto.IsActive;

            await _context.SaveChangesAsync();

            return new LaneDTO
            {
                LaneId = lane.LaneId,
                Name = lane.Name,
                BranchId = lane.BranchId,
                IsActive = lane.IsActive
            };
        }
    }
}
