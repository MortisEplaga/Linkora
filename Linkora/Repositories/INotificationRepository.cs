using Linkora.Models;

namespace Linkora.Repositories
{
    public interface INotificationRepository
    {
        Task<int> CreateAsync(int userId, int? fromUserId, int? productId, string message);
        Task<List<NotificationViewModel>> GetByUserAsync(int userId, int count = 20);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkReadAsync(int notificationId, int userId);
        Task MarkAllReadAsync(int userId);
        Task NotifySubscribersAsync(int authorId, int productId, string productName, string authorName);
    }
}
