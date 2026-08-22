using Linkora.Models;

namespace Linkora.Repositories
{
    public interface INotificationRepository
    {
        Task<int> CreateAsync(int userId, int? fromUserId, int? productId, string text);
        Task<List<(int NotificationId, int UserId)>> CreateForSubscribersAsync(int authorId, int productId, string text);
        Task<List<NotificationViewModel>> GetByUserAsync(int userId);
        Task<List<string>> GetUnreadTextsAsync(int userId);
        Task MarkReadAsync(int notificationId, int userId);
        Task MarkAllReadAsync(int userId);
    }
}