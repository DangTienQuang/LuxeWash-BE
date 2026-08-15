#pragma warning disable CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
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

            FirebaseMessaging messaging;
            try
            {
                messaging = FirebaseMessaging.DefaultInstance;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex,
                    "Firebase is not initialized. Push notification for User {UserId} was not sent.",
                    request.UserId);
                return false;
            }

            var successCount = 0;
            var failureCount = 0;
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
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High,
                        Notification = new AndroidNotification
                        {
                            ChannelId = "overload-alerts",
                            Sound = "default"
                        }
                    },
                    Data = fcmData,
                    Webpush = new WebpushConfig
                    {
                        FcmOptions = new WebpushFcmOptions
                        {
                            Link = "https://luxewash.vn/bookings/pending" // Example link for web push
                        }
                    }
                };

                try
                {
                    string response = await messaging.SendAsync(message);
                    successCount++;
                    _logger.LogInformation(
                        "Successfully sent FCM message to token record {TokenId}: {Response}",
                        userToken.Id, response);
                }
                catch (FirebaseMessagingException ex)
                {
                    failureCount++;
                    _logger.LogError(ex,
                        "Error sending FCM push notification to User {UserId}, token record {TokenId}",
                        request.UserId, userToken.Id);
                    
                    if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered || 
                        ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                    {
                        tokensToRemove.Add(userToken);
                    }
                }
                catch (System.Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex,
                        "Unexpected error sending FCM to User {UserId}, token record {TokenId}",
                        request.UserId, userToken.Id);
                }
            }

            if (tokensToRemove.Any())
            {
                _context.UserFcmTokens.RemoveRange(tokensToRemove);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} invalid FCM tokens for User {UserId}", tokensToRemove.Count, request.UserId);
            }

            if (failureCount > 0)
            {
                _logger.LogWarning(
                    "FCM send completed for User {UserId}: {SuccessCount} accepted, {FailureCount} failed.",
                    request.UserId, successCount, failureCount);
            }

            // A customer may have several devices. Treat the notification as sent
            // when FCM accepted it for at least one currently registered device.
            return successCount > 0;
        }

        public async Task RegisterTokenAsync(int userId, string token)
        {
            var existing = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.Token == token);

            if (existing == null)
            {
                _context.UserFcmTokens.Add(new UserFcmToken
                {
                    UserId = userId,
                    Token = token,
                    CreatedAt = System.DateTime.UtcNow,
                    LastUsedAt = System.DateTime.UtcNow
                });
            }
            else
            {
                // Reassign token to current user if it belonged to someone else (e.g. device reuse)
                if (existing.UserId != userId)
                    existing.UserId = userId;

                existing.LastUsedAt = System.DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("FCM token registered for User {UserId}", userId);
        }

        public async Task RemoveTokenAsync(int userId, string token)
        {
            var existing = await _context.UserFcmTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token);

            if (existing != null)
            {
                _context.UserFcmTokens.Remove(existing);
                await _context.SaveChangesAsync();
                _logger.LogInformation("FCM token removed for User {UserId}", userId);
            }
        }
    }
}

#pragma warning restore CS8600, CS8601, CS8602, CS8604, CS8625, CS8629, CS0168, CS0618
