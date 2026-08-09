using System.Collections.Generic;
using System.Threading.Tasks;
using AutoWashPro.BLL.DTOs;
namespace AutoWashPro.BLL.Services
{
    public interface IVoucherService
    {
        Task<List<VoucherResponseDTO>> GetMyVouchersAsync(int userId);
        Task RedeemVoucherAsync(int userId, int voucherId);
        Task<List<AdminVoucherDTO>> GetAllVouchersAsync();
        Task GrantVouchersAsync(int voucherId, List<int> userIds);
        Task<AdminVoucherDTO> CreateVoucherAsync(CreateOrUpdateVoucherDTO request);
        Task<AdminVoucherDTO> UpdateVoucherAsync(int id, CreateOrUpdateVoucherDTO request);
        Task<bool> DeleteVoucherAsync(int id);
        Task GenerateCompensationVoucherAsync(int userId);
        Task<bool> ConsumePhysicalVoucherAsync(int userId, string voucherCode);
    }
}
