using ChatApp.Models;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace ChatApp.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessageToGroup(MessageDetails message)
        {
            if (string.IsNullOrEmpty(message.MessageId))
            {
                message.MessageId = Guid.NewGuid().ToString(); // 🔧 Auto-generate if missing
            }

            //await Clients.Group(groupName).SendAsync("ReceiveMessage", groupName, user, message, messageId, messageTime);



            await Clients.Group(message.To).SendAsync("ReceiveMessage", message);
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task Typing(string groupName, string username)
        {
            await Clients.Group(groupName).SendAsync("ReceiveTyping", username);
        }
    }


    }