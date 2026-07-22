using AutoWashPro.BLL.DTOs;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IPushNotificationService
    {
        Task<bool> SendPushNotificationAsync(PushNotificationRequest request);
    }
}
