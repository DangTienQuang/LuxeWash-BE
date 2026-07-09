using System.Security.Claims;
using AutoWashPro.BLL.Exceptions;
using System;

namespace BLL.Helpers
{
    public static class ClaimHelper
    {
        public static int GetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new Exception("Unauthorized: User ID not found in token.");

            if (!int.TryParse(userIdClaim, out int userId))
                throw new Exception("Unauthorized: Invalid User ID.");

            return userId;
        }

        public static string GetRole(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        public static string GetUserRole(ClaimsPrincipal user)
        {
            return GetRole(user);
        }

        public static string GetUserPhone(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.MobilePhone)?.Value ?? string.Empty;
        }
    }
}
