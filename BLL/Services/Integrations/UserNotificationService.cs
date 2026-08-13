using AutoWashPro.BLL.DTOs;
using Microsoft.AspNetCore.SignalR;
using AutoWashPro.BLL.Services.Interface;
using AutoWashPro.DAL.Data;
using AutoWashPro.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Services
{
    public class UserNotificationService : IUserNotificationService
    {
        private readonly AutoWashDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<AutoWashPro.BLL.Hubs.NotificationHub> _hubContext;

        public UserNotificationService(
            AutoWashDbContext context, 
            IPushNotificationService pushNotificationService,
            Microsoft.AspNetCore.SignalR.IHubContext<AutoWashPro.BLL.Hubs.NotificationHub> hubContext)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _hubContext = hubContext;
        }

        public async Task<List<UserNotificationDTO>> GetMyNotificationsAsync(int userId)
        {
            return await _context.UserNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new UserNotificationDTO
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Type = n.Type,
                    ReferenceId = n.ReferenceId,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.UserNotifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var unreadNotifications = await _context.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task CreateNotificationAsync(int userId, string title, string body, string type, string? referenceId = null)
        {
            await CreateNotificationInternalAsync(userId, title, body, type, referenceId, sendPush: true);
        }

        public async Task CreateInAppNotificationAsync(int userId, string title, string body, string type, string? referenceId = null)
        {
            await CreateNotificationInternalAsync(userId, title, body, type, referenceId, sendPush: false);
        }

        private async Task CreateNotificationInternalAsync(
            int userId,
            string title,
            string body,
            string type,
            string? referenceId,
            bool sendPush)
        {
            var notification = new UserNotification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserNotifications.Add(notification);
            await _context.SaveChangesAsync();

            try
            {
                // Send SignalR real-time notification
                var notificationDto = new UserNotificationDTO
                {
                    Id = notification.Id,
                    Title = notification.Title,
                    Body = notification.Body,
                    Type = notification.Type,
                    ReferenceId = notification.ReferenceId,
                    IsRead = notification.IsRead,
                    CreatedAt = notification.CreatedAt
                };
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notificationDto);
            }
            catch (Exception)
            {
                // Log exception if signalR fails
            }

            if (!sendPush)
            {
                return;
            }

            try
            {
                // Send FCM push notification asynchronously
                await _pushNotificationService.SendPushNotificationAsync(new PushNotificationRequest
                {
                    UserId = userId,
                    Title = title,
                    Body = body,
                    Data = new Dictionary<string, string>
                    {
                        { "type", type },
                        { "referenceId", referenceId ?? string.Empty }
                    }
                });
            }
            catch (Exception)
            {
                // Log exception if push fails, but don't stop the flow
            }
        }

        public async Task CreateNotificationsBulkAsync(List<int> userIds, string title, string body, string type, string? referenceId = null)
        {
            if (userIds == null || !userIds.Any()) return;

            var notifications = userIds.Select(userId => new UserNotification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.UserNotifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Run networking in background to prevent blocking HTTP response
            _ = Task.Run(async () =>
            {
                var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
                await Parallel.ForEachAsync(notifications, options, async (notification, token) =>
                {
                    try
                    {
                        var notificationDto = new UserNotificationDTO
                        {
                            Id = notification.Id,
                            Title = notification.Title,
                            Body = notification.Body,
                            Type = notification.Type,
                            ReferenceId = notification.ReferenceId,
                            IsRead = notification.IsRead,
                            CreatedAt = notification.CreatedAt
                        };
                        await _hubContext.Clients.User(notification.UserId.ToString()).SendAsync("ReceiveNotification", notificationDto);
                    }
                    catch { }

                    try
                    {
                        await _pushNotificationService.SendPushNotificationAsync(new PushNotificationRequest
                        {
                            UserId = notification.UserId,
                            Title = notification.Title,
                            Body = notification.Body,
                            Data = new Dictionary<string, string>
                            {
                                { "type", notification.Type },
                                { "referenceId", notification.ReferenceId ?? string.Empty }
                            }
                        });
                    }
                    catch { }
                });
            });
        }
    }
}
