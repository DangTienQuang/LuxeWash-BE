using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;

namespace AutoWashPro.BLL.Services
{
    public interface IUserService
    {

        Task<UserProfileDTO> GetProfileAsync(int userId);
        Task<bool> UpdateProfileAsync(int userId, UpdateUserProfileDTO request);
        Task<bool> DeleteAccountAsync(int userId);


        Task<PagedResultDTO<UserAdminSummaryDTO>> GetAllCustomersAsync(int page, int pageSize, string? searchKeyword, string? statusFilter);
        Task<UserProfileDTO> GetCustomerDetailByAdminAsync(int customerId);
        Task<bool> UpdateCustomerStatusAsync(int customerId, string newStatus);

        Task SyncCustomerProfilePointsAsync();
    }
}