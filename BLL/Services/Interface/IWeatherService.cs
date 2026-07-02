using System.Threading.Tasks;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IWeatherService
    {
        Task<bool> IsRainingNowAsync();
        Task<bool> IsProlongedRainAsync(Branch branch);
    }
}
