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

        // Подключаемся к комнате диалога
        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        }

        // Отправка сообщения
        public async Task SendMessage(int conversationId, string text)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return;
            var userId = int.Parse(userIdStr);
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
                sentAt = DateTime.UtcNow.ToString("o"),
                isRead = false,
            };

            // Шлём всем в комнате
            await Clients.Group($"conv_{conversationId}").SendAsync("ReceiveMessage", payload);

            // Обновляем счётчик непрочитанных у получателя
            await Clients.User(userId.ToString()).SendAsync("UnreadCountChanged");
        }

        // Пометить прочитанным при открытии чата
        public async Task MarkRead(int conversationId)
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr == null) return;
            var userId = int.Parse(userIdStr);
            await _messageRepository.MarkReadAsync(conversationId, userId);
            await Clients.User(userId.ToString()).SendAsync("UnreadCountChanged");
        }

        // Для адресации по userId
        public override async Task OnConnectedAsync()
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdStr != null)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userIdStr}");
            await base.OnConnectedAsync();
        }
    }
}