using AutoWashPro.BLL.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services.Interface
{
    public interface IUserNotificationService
    {
        Task<List<UserNotificationDTO>> GetMyNotificationsAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
        Task CreateNotificationAsync(int userId, string title, string body, string type, string? referenceId = null);
        Task CreateInAppNotificationAsync(int userId, string title, string body, string type, string? referenceId = null);
        Task CreateNotificationsBulkAsync(List<int> userIds, string title, string body, string type, string? referenceId = null);
    }
}
