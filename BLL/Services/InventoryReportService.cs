using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Exceptions;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class InventoryReportService : IInventoryReportService
    {
        private readonly AutoWashDbContext _context;

        public InventoryReportService(AutoWashDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryReportDTO> GetAdminProfitReportAsync(DateTime? from, DateTime? to, int? branchId)
        {
            return await BuildReportAsync(from, to, branchId);
        }

        public async Task<InventoryReportDTO> GetManagerProfitReportAsync(int managerUserId, DateTime? from, DateTime? to)
        {
            var branchId = await _context.EmployeeProfiles
                .Where(e => e.EmployeeId == managerUserId)
                .Select(e => e.BranchId)
                .FirstOrDefaultAsync();

            if (!branchId.HasValue)
            {
                throw new BadRequestException("Manager is not assigned to a branch.");
            }

            return await BuildReportAsync(from, to, branchId.Value);
        }

        private async Task<InventoryReportDTO> BuildReportAsync(DateTime? from, DateTime? to, int? branchId)
        {
            var fromDate = from ?? DateTime.MinValue;
            var toDate = to ?? DateTime.MaxValue;

            var completedBookings = _context.Bookings
                .Where(b => b.Status == "Completed"
                    && b.ScheduledTime >= fromDate
                    && b.ScheduledTime <= toDate
                    && (branchId == null || b.BranchId == branchId));

            var bookingIds = completedBookings.Select(b => b.BookingId);
            var revenue = await completedBookings.SumAsync(b => (decimal?)b.FinalAmount) ?? 0;
            var bookingCount = await completedBookings.CountAsync();

            var materialCost = await _context.BookingMaterialUsages
                .Where(u => bookingIds.Contains(u.BookingId))
                .SumAsync(u => (decimal?)u.CostAmount) ?? 0;

            var grossProfit = revenue - materialCost;
            var grossMargin = revenue > 0 ? decimal.Round(grossProfit / revenue * 100, 2) : 0;

            return new InventoryReportDTO
            {
                Revenue = revenue,
                MaterialCost = materialCost,
                GrossProfit = grossProfit,
                GrossMargin = grossMargin,
                CompletedBookings = bookingCount
            };
        }
    }
}
