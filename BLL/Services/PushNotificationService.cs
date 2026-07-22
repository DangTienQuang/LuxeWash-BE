using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Services.Interface;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;
using System.Collections.Generic;
using System.Linq;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoWashPro.BLL.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly ILogger<PushNotificationService> _logger;
        private readonly AutoWashDbContext _context;

        public PushNotificationService(ILogger<PushNotificationService> logger, AutoWashDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<bool> SendPushNotificationAsync(PushNotificationRequest request)
        {
            _logger.LogInformation(">>> [PUSH NOTIFICATION INITIATED FOR USER {UserId}] <<<", request.UserId);
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId);
            
            if (user == null || string.IsNullOrEmpty(user.FcmToken))
            {
                _logger.LogWarning("Cannot send push notification. User {UserId} not found or missing FcmToken.", request.UserId);
                return false;
            }

            var message = new Message()
            {
                Token = user.FcmToken,
                Notification = new Notification
                {
                    Title = request.Title,
                    Body = request.Body
                },
                Data = new Dictionary<string, string>
                {
                    { "payload", JsonSerializer.Serialize(request.Data) }
                }
            };

            try
            {
                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Successfully sent message via FCM: {Response}", response);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM push notification to User {UserId}", request.UserId);
                return false;
            }
        }
    }
}
