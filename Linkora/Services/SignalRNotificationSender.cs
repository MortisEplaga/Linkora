using Linkora.Hubs;
using Linkora.Models;
using Microsoft.AspNetCore.SignalR;

namespace Linkora.Services
{
    public interface INotificationRealTimeSender
    {
        Task SendAsync(NotificationDispatch notification);
    }
    public class SignalRNotificationSender : INotificationRealTimeSender
    {
        private readonly IHubContext<MessageHub> _hubContext;

        public SignalRNotificationSender(IHubContext<MessageHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task SendAsync(NotificationDispatch notification) => _hubContext
                .Clients.Group($"user_{notification.TargetUserId}")
                .SendAsync("NotificationReceived", new
                {
                    id = notification.Id,
                    text = notification.Text,
                    fromUserId = notification.FromUserId,
                    productId = notification.ProductId,
                    createdAt = notification.CreatedAt.ToString("dd MMM, HH:mm"),
                    isRead = false,
                    fromUserAvatar = (string?)null,
                    productName = notification.ProductName,
                    productImage = (string?)null,
                });
    }
}