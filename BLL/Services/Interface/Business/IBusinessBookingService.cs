using AutoWashPro.BLL.DTOs;
using BLL.DTOs;
using BLL.DTOs.Business;
using BLL.DTOs.Fleet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace BLL.Services.Interface
{
    public interface IBusinessBookingService
    {
        Task<MultiVehicleBookingResponseDTO> CreateBusinessBookingAsync(int businessUserId, CreateBusinessBookingDTO dto);
        Task<List<FleetVehicleDTO>> GetActiveFleetVehiclesAsync(int businessUserId);
        Task<List<BusinessBookingListDTO>> GetBookingsAsync(int businessUserId);
        Task<BusinessBookingDetailDTO> GetBookingDetailAsync(int businessUserId, int bookingId);
        Task CancelBookingAsync(int businessUserId, int bookingId);
        Task<FleetWashLogDTO> CheckInAsync(int bookingId);
        Task<FleetCheckInResponseDTO> WalkInAsync(FleetWalkInDTO dto);
        Task WalkOutAsync(int washLogId);
        Task StartProcessingAsync(int washLogId, int staffUserId, StartFleetWashDTO dto);
        Task<List<CurrentFleetVehicleDTO>> GetCurrentVehiclesAsync();
        Task<FleetCheckoutResponseDTO> CheckOutAsync(int washLogId);
        Task<InvoiceDTO> GetInvoiceByBookingAsync(int bookingId);
        Task<List<FleetWashHistoryDTO>> GetFleetWashHistoryAsync(int businessUserId, FleetHistoryFilterDTO filter);
        Task<FleetDashboardDTO> GetDashboardAsync(int businessUserId);
        Task<List<InvoiceListDTO>> GetInvoicesAsync(int businessUserId);
        Task<InvoiceDetailDTO> GetInvoiceDetailAsync(int businessUserId, int invoiceId);
        Task<MonthlyStatementDTO> GetMonthlyStatementAsync(int businessUserId, int year, int month);
        Task AssignLaneAsync(int washLogId, AssignLaneDTO dto);
        Task<List<BusinessVehicleStatusDTO>> GetActiveVehiclesOnFloorAsync(int businessUserId);
        Task<List<BusinessVehicleStatusDTO>> GetVehiclesByStatusAsync(int businessUserId, string? status);
        Task<List<DTOs.Business.TimeSlotResponseDTO>> GetAvailableSlotsForBusinessAsync(int businessUserId, CheckBusinessSlotsRequestDTO request);
        Task<RescheduleBusinessResponseDTO> RescheduleBookingAsync(int businessUserId, RescheduleBusinessBookingDTO dto);
    }
}
