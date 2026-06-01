using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SignalR.TsGeneration.Hubs
{
    /// <summary>
    /// Notification hub — server-to-client push notifications with acknowledgment.
    /// </summary>
    public class NotificationHub : Hub
    {
        /// <summary>Sends a notification to a specific user.</summary>
        public async Task SendNotification (string userId, string title, string body, string? actionUrl)
        {
            await Clients.User (userId).SendAsync ("ReceiveNotification", new
            {
                title,
                body,
                actionUrl,
                timestamp = DateTimeOffset.UtcNow
            });
        }

        /// <summary>Marks a notification as read.</summary>
        public async Task MarkAsRead (string notificationId)
        {
            await Clients.Caller.SendAsync ("MarkedAsRead", notificationId);
        }

        /// <summary>Gets unread notification count.</summary>
        public async Task<int> GetUnreadCount (string userId)
        {
            return 5; // mock implementation
        }

        /// <summary>Subscribes to notification channels.</summary>
        public async Task Subscribe (string[] channels)
        {
            foreach (var channel in channels)
                await Groups.AddToGroupAsync (Context.ConnectionId, $"channel:{channel}");
            await Clients.Caller.SendAsync ("Subscribed", channels);
        }
    }
}
