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
    public interface IBusinessService
    {
        Task<RegisterBusinessUserResponse> RegisterBusinessUserAsync(RegisterBusinessUserRequest request);
        Task<BusinessProfileResponseDTO?> GetByUserIdAsync(int userId);
        Task ReviewBusinessProfileAsync(int reviewerId, ReviewBusinessProfileDTO dto);
        Task<List<PendingBusinessApplicationDTO>> GetPendingBusinessApplicationsAsync();
        Task<PendingBusinessApplicationDTO?> GetBusinessApplicationDetailAsync(int businessProfileId);
        Task<InvoiceExportDTO> GetInvoiceExportAsync(int invoiceId);
        Task<List<BillingBusinessDTO>> GetBillingBusinessesAsync();
        Task<int> GenerateMonthlyInvoiceAsync(int businessProfileId, int year, int month);
    }
}
