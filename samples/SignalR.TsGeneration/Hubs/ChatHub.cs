using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace SignalR.TsGeneration.Hubs
{
    /// <summary>
    /// Chat hub — real-time messaging with rooms and typing indicators.
    /// </summary>
    public class ChatHub : Hub
    {
        /// <summary>Sends a message to all clients in a room.</summary>
        public async Task SendMessage (string room, string user, string message)
        {
            await Clients.Group (room).SendAsync ("ReceiveMessage", user, message);
        }

        /// <summary>Joins a chat room.</summary>
        public async Task JoinRoom (string room)
        {
            await Groups.AddToGroupAsync (Context.ConnectionId, room);
            await Clients.Group (room).SendAsync ("UserJoined", Context.ConnectionId, room);
        }

        /// <summary>Leaves a chat room.</summary>
        public async Task LeaveRoom (string room)
        {
            await Groups.RemoveFromGroupAsync (Context.ConnectionId, room);
            await Clients.Group (room).SendAsync ("UserLeft", Context.ConnectionId, room);
        }

        /// <summary>Broadcasts that a user is typing.</summary>
        public async Task Typing (string room, string user, bool isTyping)
        {
            await Clients.OthersInGroup (room).SendAsync ("UserTyping", user, isTyping);
        }

        /// <summary>Gets the list of active rooms.</summary>
        public async Task<List<string>> GetRooms ()
        {
            return new List<string> { "general", "random", "tech" };
        }
    }
}
