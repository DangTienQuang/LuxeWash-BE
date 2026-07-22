using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AutoWashPro.BLL.Services; // Ensure IEmployeeService/etc if needed

using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using AutoWashPro.DAL.Data;

namespace AutoWashPro.BLL.Hubs
{
    [Authorize]
    public class LaneDisplayHub : Hub
    {
        private readonly IServiceProvider _serviceProvider;

        public LaneDisplayHub(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                // We need to resolve the user's branchId.
                // Since this is a singleton hub, we create a scope to get a scoped service.
                using var scope = _serviceProvider.CreateScope();
                // Usually employees have a branchId in the EmployeeService. We will check the context.
                var context = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
                var employeeProfile = await context.EmployeeProfiles.FirstOrDefaultAsync(e => e.EmployeeId == userId);

                if (employeeProfile != null && employeeProfile.BranchId.HasValue)
                {
                    int branchId = employeeProfile.BranchId.Value;
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}:lane-display");
                }
                else
                {
                     // If no branch assigned, maybe reject or allow just connected state
                     Context.Abort();
                }
            }
            else
            {
                Context.Abort();
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
