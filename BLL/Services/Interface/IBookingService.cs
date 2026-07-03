using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IBookingService
    {
        Task<List<TimeSlotResponseDTO>> GetAvailableSlotsAsync(int userId, CheckAvailableSlotsRequestDTO request);
        Task<CompatibilityDTO> ValidateBookingCompatibilityAsync(int userId, int branchId, int slotId, DateTime targetDate, int? vehicleId, string licensePlate, List<int> serviceIds);
        Task<CompatibilityDTO> CheckCompatibilityAsync(int userId, CheckCompatibilityRequestDTO request);
        Task<BookingResponseDTO> GetBookingByIdAsync(int userId, int bookingId);
        Task<BookingResponseDTO> CreateBookingAsync(int userId, CreateBookingDTO request);
        Task<BookingPaymentLinkResponseDTO> CreateBookingPaymentLinkAsync(int userId, int bookingId, CreateBookingPaymentLinkDTO request);
        Task<WalkInBookingResponseDTO> CreateWalkInBookingAsync(int staffId, CreateWalkInBookingDTO request);
        Task<List<AdminBookingResponseDTO>> GetAllBookingsByDateAsync(DateTime targetDate);
        Task<SmartLicensePlateResponseDTO> LookupLicensePlateAsync(string licensePlate, int branchId);
        Task<bool> UpdateBookingStatusAsync(int bookingId, string newStatus);
        Task<BookingResponseDTO> UpdateBookingStatusByLicensePlateAsync(string licensePlate, string newStatus);
        Task<List<BookingResponseDTO>> GetMyBookingsAsync(int userId);
        Task<bool> CancelBookingAsync(int userId, int bookingId);
        Task<bool> UpdateVehicleConditionAsync(int staffId, int bookingId, UpdateVehicleConditionDTO request);
        Task MarkAsNoShowAsync(int bookingId);
        Task ReportMismatchAsync(int bookingId, AutoWashPro.BLL.Enums.VehicleConditionEnum condition, int actualTypeId);
        Task ForceCancelBookingsAsync(ForceCancelRequestDTO request);
        Task<bool> SendBookingConfirmationEmailAsync(int userId, int bookingId);
        Task<BookingResponseDTO> RescheduleBookingAsync(int userId, int bookingId, RescheduleBookingDTO request);
        Task<BookingPaymentStatusDTO> GetBookingPaymentStatusAsync(int bookingId);
    }
}
