using AutoWashPro.DAL.Entities;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IDataSeedingService
    {
        Task<Booking> SeedTestBookingForAIAsync(string licensePlate = "30A-888.88");
    }
}
