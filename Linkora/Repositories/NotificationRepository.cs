using Linkora.Hubs;
using Linkora.Models;
using Linkora.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class NotificationRepository : SqlRepositoryBase, INotificationRepository
    {
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly INotificationPreferencesRepository _preferencesRepository;

        public NotificationRepository(IConfiguration configuration, IHubContext<MessageHub> hubContext, INotificationPreferencesRepository preferencesRepository) : base(configuration)
        {
            _hubContext = hubContext;
            _preferencesRepository = preferencesRepository;
        }
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

            var id = ids[0];

            await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationReceived", new
            {
                id,
                text,
                fromUserId,
                productId,
                createdAt = DateTime.UtcNow.ToString("dd MMM, HH:mm"),
                isRead = false,
                fromUserAvatar = (string?)null,
                productName = (string?)null,
                productImage = (string?)null,
            });

            return id;
        }
        private static bool IsAllowed(string text, NotificationPreferences prefs)
        {
            var category = NotificationCategorizer.Categorize(text);
            return category switch
            {
                "Deals" => prefs.Deals,
                "Reviews" => prefs.Reviews,
                "Moderation" => prefs.Moderation,
                "Account" => prefs.Account,
                "Favourites" => prefs.Favourites,
                _ => prefs.NewListings,
            };
        }
        public async Task<List<NotificationViewModel>> GetByUserAsync(int userId, int count = 20)
        {
            var prefs = await _preferencesRepository.GetAsync(userId);

            var allNotifications = await QueryAsync(
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
                    FromUserId = r.IsDBNull(2) ? null : r.GetInt32(2),
                    ProductId = r.IsDBNull(3) ? null : r.GetInt32(3),
                    Text = r.IsDBNull(4) ? "" : r.GetString(4),
                    IsRead = r.GetBoolean(5),
                    CreatedAt = r.GetDateTime(6),
                    FromUserName = r.IsDBNull(7) ? null : r.GetString(7),
                    FromUserAvatar = r.IsDBNull(8) ? null : r.GetString(8),
                    ProductName = r.IsDBNull(9) ? null : r.GetString(9),
                    ProductImage = r.IsDBNull(10) ? null : r.GetString(10),
                },
                p => p.AddWithValue("@UserId", userId));

            return allNotifications
                .Where(n => IsAllowed(n.Text, prefs))
                .Take(count)
                .ToList();
        }
        public async Task<int> GetUnreadCountAsync(int userId)
        {
            var prefs = await _preferencesRepository.GetAsync(userId);
            
            var texts = await QueryAsync<string>(
                "SELECT Text FROM Notifications WHERE UserId = @UserId AND IsRead = 0",
                r => r.IsDBNull(0) ? "" : r.GetString(0),
                p => p.AddWithValue("@UserId", userId));

            return texts.Count(t => IsAllowed(t, prefs));
        }
        public async Task MarkReadAsync(int notificationId, int userId)
        {
            await ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id AND UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@Id", notificationId);
                    p.AddWithValue("@UserId", userId);
                });
        }
        public async Task MarkAllReadAsync(int userId)
        {
            await ExecuteAsync(
                "UPDATE Notifications SET IsRead = 1 WHERE UserId = @UserId AND IsRead = 0",
                p => p.AddWithValue("@UserId", userId));
        }
        public async Task NotifySubscribersAsync(int authorId, int productId, string productName, string authorName)
        {
            await using var conn = await OpenConnectionAsync();

            await using var followersCmd = new SqlCommand(
                "SELECT FollowerId FROM Subscriptions WHERE FollowingId = @AuthorId", conn);
            followersCmd.Parameters.AddWithValue("@AuthorId", authorId);
            
            await using var r = await followersCmd.ExecuteReaderAsync();
            var followerIds = new List<int>();
            while (await r.ReadAsync())
                followerIds.Add(r.GetInt32(0));
            await r.CloseAsync();

            if (followerIds.Count == 0) return;

            var text = $"{authorName} posted a new listing: {productName}";
            foreach (var followerId in followerIds)
            {
                await using var insertCmd = new SqlCommand(@"
                    INSERT INTO Notifications (UserId, FromUserId, ProductId, Text, IsRead, CreatedAt)
                    OUTPUT INSERTED.Id
                    VALUES (@UserId, @FromUserId, @ProductId, @Text, 0, GETDATE())", conn);
                insertCmd.Parameters.AddWithValue("@UserId", followerId);
                insertCmd.Parameters.AddWithValue("@FromUserId", authorId);
                insertCmd.Parameters.AddWithValue("@ProductId", productId);
                insertCmd.Parameters.AddWithValue("@Text", text);
                var id = (int)(await insertCmd.ExecuteScalarAsync())!;

                await _hubContext.Clients.Group($"user_{followerId}").SendAsync("NotificationReceived", new
                {
                    id,
                    text,
                    fromUserId = authorId,
                    productId,
                    createdAt = DateTime.UtcNow.ToString("dd MMM, HH:mm"),
                    isRead = false,
                    fromUserAvatar = (string?)null,
                    productName,
                    productImage = (string?)null,
                });
            }
        }
    }
}