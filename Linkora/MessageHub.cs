using Linkora.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Linkora.Hubs
{
    [Authorize]
    public class MessageHub : Hub
    {
        private readonly IMessageRepository _messageRepository;
        public MessageHub(IMessageRepository messageRepository)
        {
            _messageRepository = messageRepository;
        }
        public async Task JoinConversation(int conversationId) => await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        public async Task LeaveConversation(int conversationId) => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        public async Task SendMessage(int conversationId, string text)
        {
            if (!Context.User.TryGetUserId(out int userId)) return;
            var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

            if (string.IsNullOrWhiteSpace(text)) return;

            var msgId = await _messageRepository.SendMessageAsync(conversationId, userId, text);

            var payload = new
            {
                id = msgId,
                conversationId,
                senderId = userId,
                senderName = userName,
                text,
                createdAt = DateTime.UtcNow.ToString("o"),
                isRead = false,
            };

            await Clients.Group($"conv_{conversationId}").SendAsync("ReceiveMessage", payload);

            await Clients.User(userId.ToString()).SendAsync("UnreadCountChanged");
        }
        public async Task MarkRead(int conversationId)
        {
            if (!Context.User.TryGetUserId(out int userId)) return;
            await _messageRepository.MarkReadAsync(conversationId, userId);
            await Clients.User(userId.ToString()).SendAsync("UnreadCountChanged");
        }
        public override async Task OnConnectedAsync()
        {
            if (!Context.User.TryGetUserId(out int userIdStr)) await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userIdStr}");
            await base.OnConnectedAsync();
        }
    }
}