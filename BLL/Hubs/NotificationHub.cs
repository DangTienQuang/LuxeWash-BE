using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AutoWashPro.BLL.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        // Clients can connect to this hub and receive notifications.
        // SignalR automatically uses ClaimTypes.NameIdentifier to identify users.
        
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
