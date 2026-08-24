using Linkora.Models;

namespace Linkora.Repositories
{
    public class NotificationRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), INotificationRepository
    {
        public async Task<int> CreateAsync(int userId, int? fromUserId, int? productId, string text)
        {
            var ids = await QueryAsync<int>(
                @"INSERT INTO Notifications (UserId, FromUserId, ProductId, Text, IsRead, CreatedAt)
                  OUTPUT INSERTED.Id
                  VALUES (@UserId, @FromUserId, @ProductId, @Text, 0, GETDATE())",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@FromUserId", (object?)fromUserId ?? DBNull.Value);
                    p.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);
                    p.AddWithValue("@Text", text);
                });

            return ids[0];
        }
        public async Task<List<(int NotificationId, int UserId)>> CreateForSubscribersAsync(int authorId, int productId, string text) => await QueryAsync<(int, int)>(
                @"INSERT INTO Notifications (UserId, FromUserId, ProductId, Text, IsRead, CreatedAt)
                  OUTPUT INSERTED.Id, INSERTED.UserId
                  SELECT FollowerId, @AuthorId, @ProductId, @Text, 0, GETDATE()
                  FROM Subscriptions WHERE FollowingId = @AuthorId",
                r => (r.GetInt32(0), r.GetInt32(1)),
                p =>
                {
                    p.AddWithValue("@AuthorId", authorId);
                    p.AddWithValue("@ProductId", productId);
                    p.AddWithValue("@Text", text);
                });
        public Task<List<NotificationViewModel>> GetByUserAsync(int userId) => QueryAsync(
                @"SELECT
                    n.Id, n.UserId, n.FromUserId, n.ProductId, n.Text, n.IsRead, n.CreatedAt,
                    u.UserName, u.AvatarUrl,
                    p.Name AS ProductName,
                    COALESCE(
                        (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
                         WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                        p.AvatarUrl
                    ) AS ProductImage
                  FROM Notifications n
                  LEFT JOIN Users u ON u.Id = n.FromUserId
                  LEFT JOIN Products p ON p.Id = n.ProductId
                  WHERE n.UserId = @UserId
                  ORDER BY n.CreatedAt DESC",
                r => new NotificationViewModel
                {
                    Id = r.GetInt32(0),
                    UserId = r.GetInt32(1),
                    FromUserId = r.GetInt32OrNull(2),
                    ProductId = r.GetInt32OrNull(3),
                    Text = r.GetStringOrDefault(4),
                    IsRead = r.GetBoolean(5),
                    CreatedAt = r.GetDateTime(6),
                    FromUserName = r.GetStringOrNull(7),
                    FromUserAvatar = r.GetStringOrNull(8),
                    ProductName = r.GetStringOrNull(9),
                    ProductImage = r.GetStringOrNull(10),
                },
                p => p.AddWithValue("@UserId", userId));
        public Task<List<string>> GetUnreadTextsAsync(int userId) => QueryAsync<string>(
                "SELECT Text FROM Notifications WHERE UserId = @UserId AND IsRead = 0",
                r => r.GetStringOrDefault(0),
                p => p.AddWithValue("@UserId", userId));
        public Task MarkReadAsync(int notificationId, int userId) => ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id AND UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Id", notificationId);
                    p.AddWithValue("@UserId", userId);
                });
        public Task MarkAllReadAsync(int userId) => ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE UserId = @UserId AND IsRead = 0",
                p => p.AddWithValue("@UserId", userId));
    }
}