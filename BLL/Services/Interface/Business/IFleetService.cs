using AutoWashPro.BLL.DTOs;
using BLL.DTOs.Fleet;
using DAL.Entities;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.Services.Interface
{
    public interface IFleetService
    {
        Task<FleetImportResultDTO> ImportFleetAsync(int userId, IFormFile file);
        Task<List<FleetImportBatch>> GetImportBatchesAsync();
        Task<FleetImportDetailDTO> GetImportBatchDetailAsync(int batchId);
        Task<List<FleetVehicleDTO>> GetPendingVehiclesAsync(int businessUserId);
        Task ApproveFleetVehicleAsync(int fleetVehicleId);
        Task RejectFleetVehicleAsync(int fleetVehicleId, string reason);
        Task<List<FleetHistoryDTO>> GetHistoryAsync(int businessUserId, FleetHistoryFilterDTO filter);
        Task<List<FleetQueueDTO>> GetBusinessQueueAsync(int branchId);
        Task<FleetDashboardDTO> GetDashboardAsync(int businessUserId);
        Task<List<FleetWashHistoryDTO>> GetWashHistoryAsync(int businessUserId);
        Task<byte[]> GenerateFleetTemplateAsync();
        Task<FleetTemplateDTO> GetFleetTemplateInfoAsync(string baseUrl);
        Task<List<StaffPendingVehicleDTO>> GetAllPendingVehiclesAsync(int? businessProfileId = null);
        Task<LaneDTO> CreateBusinessLaneAsync(CreateBusinessLaneDTO dto);
    }
}
