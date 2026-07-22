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
using AutoWashPro.DAL.Entities;

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
            
            var user = await _context.Users
                .Include(u => u.FcmTokens)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId);
            
            if (user == null || !user.FcmTokens.Any())
            {
                _logger.LogWarning("Cannot send push notification. User {UserId} not found or missing FcmToken.", request.UserId);
                return false;
            }

            var fcmData = new Dictionary<string, string>();
            if (request.Data != null)
            {
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var jsonString = JsonSerializer.Serialize(request.Data, options);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        fcmData[kvp.Key] = kvp.Value.ToString();
                    }
                }
            }

            bool allSuccess = true;
            var tokensToRemove = new List<UserFcmToken>();

            foreach (var userToken in user.FcmTokens)
            {
                var message = new Message()
                {
                    Token = userToken.Token,
                    Notification = new Notification
                    {
                        Title = request.Title,
                        Body = request.Body
                    },
                    Data = fcmData,
                    Webpush = new WebpushConfig
                    {
                        FcmOptions = new WebpushFcmOptions
                        {
                            Link = "https://smartwash.vn/bookings/pending" // Example link for web push
                        }
                    }
                };

                try
                {
                    string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                    _logger.LogInformation("Successfully sent message via FCM to token {Token}: {Response}", userToken.Token, response);
                }
                catch (FirebaseMessagingException ex)
                {
                    _logger.LogError(ex, "Error sending FCM push notification to User {UserId} with token {Token}", request.UserId, userToken.Token);
                    
                    if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || 
                        ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                    {
                        tokensToRemove.Add(userToken);
                    }
                    else
                    {
                        allSuccess = false;
                    }
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error sending FCM to User {UserId} with token {Token}", request.UserId, userToken.Token);
                    allSuccess = false;
                }
            }

            if (tokensToRemove.Any())
            {
                _context.UserFcmTokens.RemoveRange(tokensToRemove);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} invalid FCM tokens for User {UserId}", tokensToRemove.Count, request.UserId);
            }

            return allSuccess;
        }
    }
}
