using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly string _connectionString;

        public NotificationRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<int> CreateAsync(int userId, int? fromUserId, int? productId, string message)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Notifications (UserId, FromUserId, ProductId, Message, IsRead, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @FromUserId, @ProductId, @Message, 0, GETDATE())", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@FromUserId", (object?)fromUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Message", message);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task<List<NotificationViewModel>> GetByUserAsync(int userId, int count = 20)
        {
            var result = new List<NotificationViewModel>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
                SELECT TOP {count}
                    n.Id, n.UserId, n.FromUserId, n.ProductId, n.Message, n.IsRead, n.CreatedAt,
                    u.UserName, u.AvatarImagePath,
                    p.Name AS ProductName,
                    COALESCE(
                        (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
                         WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                        p.AvatarImagePath
                    ) AS ProductImage
                FROM Notifications n
                LEFT JOIN Users u ON u.Id = n.FromUserId
                LEFT JOIN Products p ON p.Id = n.ProductId
                WHERE n.UserId = @UserId
                ORDER BY n.CreatedAt DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new NotificationViewModel
                {
                    Id = r.GetInt32(0),
                    UserId = r.GetInt32(1),
                    FromUserId = r.IsDBNull(2) ? null : r.GetInt32(2),
                    ProductId = r.IsDBNull(3) ? null : r.GetInt32(3),
                    Message = r.IsDBNull(4) ? "" : r.GetString(4),
                    IsRead = r.GetBoolean(5),
                    CreatedAt = r.GetDateTime(6),
                    FromUserName = r.IsDBNull(7) ? null : r.GetString(7),
                    FromUserAvatar = r.IsDBNull(8) ? null : r.GetString(8),
                    ProductName = r.IsDBNull(9) ? null : r.GetString(9),
                    ProductImage = r.IsDBNull(10) ? null : r.GetString(10),
                });
            return result;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM Notifications WHERE UserId = @UserId AND IsRead = 0", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task MarkReadAsync(int notificationId, int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id AND UserId = @UserId", conn);
            cmd.Parameters.AddWithValue("@Id", notificationId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkAllReadAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Notifications SET IsRead = 1 WHERE UserId = @UserId AND IsRead = 0", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task NotifySubscribersAsync(int authorId, int productId, string productName, string authorName)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Get all followers of the author
            await using var followersCmd = new SqlCommand(
                "SELECT FollowerId FROM Subscriptions WHERE FollowingId = @AuthorId", conn);
            followersCmd.Parameters.AddWithValue("@AuthorId", authorId);
            await using var r = await followersCmd.ExecuteReaderAsync();
            var followerIds = new List<int>();
            while (await r.ReadAsync())
                followerIds.Add(r.GetInt32(0));
            await r.CloseAsync();

            if (!followerIds.Any()) return;

            var message = $"{authorName} posted a new listing: {productName}";
            foreach (var followerId in followerIds)
            {
                await using var insertCmd = new SqlCommand(@"
                    INSERT INTO Notifications (UserId, FromUserId, ProductId, Message, IsRead, CreatedAt)
                    VALUES (@UserId, @FromUserId, @ProductId, @Message, 0, GETDATE())", conn);
                insertCmd.Parameters.AddWithValue("@UserId", followerId);
                insertCmd.Parameters.AddWithValue("@FromUserId", authorId);
                insertCmd.Parameters.AddWithValue("@ProductId", productId);
                insertCmd.Parameters.AddWithValue("@Message", message);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }
}