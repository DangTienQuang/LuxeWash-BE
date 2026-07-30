using AutoWashPro.BLL.DTOs;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IPushNotificationService
    {
        Task<bool> SendPushNotificationAsync(PushNotificationRequest request);
        Task RegisterTokenAsync(int userId, string token);
        Task RemoveTokenAsync(int userId, string token);
    }
}
