using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public interface IMessageRepository
    {
        Task<List<Conversation>> GetConversationsAsync(int userId);
        Task<Conversation?> GetConversationAsync(int conversationId, int userId);
        Task<int> GetOrCreateConversationAsync(int productId, int buyerId, int sellerId);
        Task<List<Message>> GetMessagesAsync(int conversationId, int userId);
        Task<int> SendMessageAsync(int conversationId, int senderId, string text);
        Task MarkReadAsync(int conversationId, int userId);
        Task<int> GetUnreadCountAsync(int userId);
    }

    public class MessageRepository : IMessageRepository
    {
        private readonly string _connectionString;

        public MessageRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<Conversation>> GetConversationsAsync(int userId)
        {
            var result = new List<Conversation>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT c.Id, c.ProductId, c.BuyerId, c.SellerId, c.IsSystem, c.CreatedAt,
                       p.Name, p.AvatarImagePath,
                       CASE WHEN c.BuyerId = @UserId THEN su.UserName ELSE bu.UserName END,
                       CASE WHEN c.BuyerId = @UserId THEN su.AvatarImagePath ELSE bu.AvatarImagePath END,
                       CASE WHEN c.BuyerId = @UserId THEN c.SellerId ELSE c.BuyerId END,
                       (SELECT TOP 1 Text FROM Messages WHERE ConversationId = c.Id ORDER BY SentAt DESC),
                       (SELECT TOP 1 SentAt FROM Messages WHERE ConversationId = c.Id ORDER BY SentAt DESC),
                       (SELECT COUNT(*) FROM Messages WHERE ConversationId = c.Id AND IsRead = 0 AND SenderId != @UserId)
                FROM Conversations c
                LEFT JOIN Products p ON p.Id = c.ProductId
                LEFT JOIN Users bu ON bu.Id = c.BuyerId
                LEFT JOIN Users su ON su.Id = c.SellerId
                WHERE c.BuyerId = @UserId OR c.SellerId = @UserId
                ORDER BY (SELECT TOP 1 SentAt FROM Messages WHERE ConversationId = c.Id ORDER BY SentAt DESC) DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new Conversation
                {
                    Id = r.GetInt32(0),
                    ProductId = r.IsDBNull(1) ? null : r.GetInt32(1),
                    BuyerId = r.GetInt32(2),
                    SellerId = r.GetInt32(3),
                    IsSystem = r.GetBoolean(4),
                    CreatedAt = r.GetDateTime(5),
                    ProductName = r.IsDBNull(6) ? null : r.GetString(6),
                    ProductImage = r.IsDBNull(7) ? null : r.GetString(7),
                    OtherUserName = r.IsDBNull(8) ? null : r.GetString(8),
                    OtherUserAvatar = r.IsDBNull(9) ? null : r.GetString(9),
                    OtherUserId = r.IsDBNull(10) ? 0 : r.GetInt32(10),
                    LastMessage = r.IsDBNull(11) ? null : r.GetString(11),
                    LastMessageAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
                    UnreadCount = r.IsDBNull(13) ? 0 : r.GetInt32(13),
                });
            return result;
        }

        public async Task<Conversation?> GetConversationAsync(int conversationId, int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT c.Id, c.ProductId, c.BuyerId, c.SellerId, c.IsSystem, c.CreatedAt,
                       p.Name, p.AvatarImagePath,
                       CASE WHEN c.BuyerId = @UserId THEN su.UserName ELSE bu.UserName END,
                       CASE WHEN c.BuyerId = @UserId THEN su.AvatarImagePath ELSE bu.AvatarImagePath END,
                       CASE WHEN c.BuyerId = @UserId THEN c.SellerId ELSE c.BuyerId END
                FROM Conversations c
                LEFT JOIN Products p ON p.Id = c.ProductId
                LEFT JOIN Users bu ON bu.Id = c.BuyerId
                LEFT JOIN Users su ON su.Id = c.SellerId
                WHERE c.Id = @Id AND (c.BuyerId = @UserId OR c.SellerId = @UserId)", conn);
            cmd.Parameters.AddWithValue("@Id", conversationId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            return new Conversation
            {
                Id = r.GetInt32(0),
                ProductId = r.IsDBNull(1) ? null : r.GetInt32(1),
                BuyerId = r.GetInt32(2),
                SellerId = r.GetInt32(3),
                IsSystem = r.GetBoolean(4),
                CreatedAt = r.GetDateTime(5),
                ProductName = r.IsDBNull(6) ? null : r.GetString(6),
                ProductImage = r.IsDBNull(7) ? null : r.GetString(7),
                OtherUserName = r.IsDBNull(8) ? null : r.GetString(8),
                OtherUserAvatar = r.IsDBNull(9) ? null : r.GetString(9),
                OtherUserId = r.IsDBNull(10) ? 0 : r.GetInt32(10),
            };
        }

        public async Task<int> GetOrCreateConversationAsync(int productId, int buyerId, int sellerId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Ищем существующий
            await using var findCmd = new SqlCommand(@"
                SELECT Id FROM Conversations
                WHERE ProductId = @ProductId AND BuyerId = @BuyerId AND SellerId = @SellerId", conn);
            findCmd.Parameters.AddWithValue("@ProductId", productId);
            findCmd.Parameters.AddWithValue("@BuyerId", buyerId);
            findCmd.Parameters.AddWithValue("@SellerId", sellerId);
            var existing = await findCmd.ExecuteScalarAsync();
            if (existing != null) return (int)existing;

            // Создаём новый
            await using var createCmd = new SqlCommand(@"
                INSERT INTO Conversations (ProductId, BuyerId, SellerId, CreatedAt, IsSystem)
                OUTPUT INSERTED.Id
                VALUES (@ProductId, @BuyerId, @SellerId, GETDATE(), 0)", conn);
            createCmd.Parameters.AddWithValue("@ProductId", productId);
            createCmd.Parameters.AddWithValue("@BuyerId", buyerId);
            createCmd.Parameters.AddWithValue("@SellerId", sellerId);
            return (int)(await createCmd.ExecuteScalarAsync())!;
        }

        public async Task<List<Message>> GetMessagesAsync(int conversationId, int userId)
        {
            var result = new List<Message>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT m.Id, m.ConversationId, m.SenderId, m.Text, m.SentAt, m.IsRead,
                       u.UserName, u.AvatarImagePath
                FROM Messages m
                LEFT JOIN Users u ON u.Id = m.SenderId
                WHERE m.ConversationId = @ConvId
                ORDER BY m.SentAt ASC", conn);
            cmd.Parameters.AddWithValue("@ConvId", conversationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new Message
                {
                    Id = r.GetInt32(0),
                    ConversationId = r.GetInt32(1),
                    SenderId = r.IsDBNull(2) ? null : r.GetInt32(2),
                    Text = r.GetString(3),
                    SentAt = r.GetDateTime(4),
                    IsRead = r.GetBoolean(5),
                    SenderName = r.IsDBNull(6) ? null : r.GetString(6),
                    SenderAvatar = r.IsDBNull(7) ? null : r.GetString(7),
                });
            return result;
        }

        public async Task<int> SendMessageAsync(int conversationId, int senderId, string text)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Messages (ConversationId, SenderId, Text, SentAt, IsRead)
                OUTPUT INSERTED.Id
                VALUES (@ConvId, @SenderId, @Text, GETDATE(), 0)", conn);
            cmd.Parameters.AddWithValue("@ConvId", conversationId);
            cmd.Parameters.AddWithValue("@SenderId", senderId);
            cmd.Parameters.AddWithValue("@Text", text);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task MarkReadAsync(int conversationId, int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                UPDATE Messages SET IsRead = 1
                WHERE ConversationId = @ConvId AND SenderId != @UserId AND IsRead = 0", conn);
            cmd.Parameters.AddWithValue("@ConvId", conversationId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Messages m
                JOIN Conversations c ON c.Id = m.ConversationId
                WHERE (c.BuyerId = @UserId OR c.SellerId = @UserId)
                  AND m.SenderId != @UserId AND m.IsRead = 0", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
    }
}