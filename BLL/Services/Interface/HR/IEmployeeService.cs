using AutoWashPro.BLL.DTOs;
using System.Threading.Tasks;
namespace AutoWashPro.BLL.Services
{
    public interface IEmployeeService
    {
        Task<EmployeeProfileDTO> CreateEmployeeAsync(CreateEmployeeDTO createDto);
        Task<bool> TransferEmployeeAsync(int employeeId, TransferEmployeeDTO transferDto);
    }
}
