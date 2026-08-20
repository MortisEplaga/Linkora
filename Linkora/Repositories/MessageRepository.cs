using Linkora.Models;
using Linkora.Repositories;
using Microsoft.Data.SqlClient;

public class MessageRepository : IMessageRepository
{
    private readonly string _connectionString;

    public MessageRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }
    public async Task<int> GetOrCreateSupportConversationAsync(int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        const int systemAccountId = 3;

        await using var findCmd = new SqlCommand(@"
                SELECT Id FROM Conversations 
                WHERE BuyerId = @UserId AND IsSupport = 1", conn);
        findCmd.Parameters.AddWithValue("@UserId", userId);

        var existing = await findCmd.ExecuteScalarAsync();
        if (existing != null) return (int)existing;

        await using var createCmd = new SqlCommand(@"
                INSERT INTO Conversations (ProductId, BuyerId, SellerId, CreatedAt, IsSystem, IsSupport)
                OUTPUT INSERTED.Id
                VALUES (NULL, @UserId, @SystemId, GETDATE(), 0, 1)", conn);
        createCmd.Parameters.AddWithValue("@UserId", userId);
        createCmd.Parameters.AddWithValue("@SystemId", systemAccountId);

        return (int)(await createCmd.ExecuteScalarAsync())!;
    }
    public async Task<string> GetUserStatusAsync(int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT Role FROM Users WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            string role = r.IsDBNull(0) ? null : r.GetString(0);
            return role;
        }
        return "user";
    }

    public async Task<bool> CanReviewAsync(int conversationId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
        SELECT c.Id, c.ProductId, c.BuyerId, c.SellerId
        FROM Conversations c
        WHERE c.Id = @ConvId AND c.IsSystem = 1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ConvId", conversationId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return false;

        var productId = reader.GetInt32(1);
        var buyerId = reader.GetInt32(2);
        var sellerId = reader.GetInt32(3);

        int targetUserId = (userId == buyerId) ? sellerId : (userId == sellerId ? buyerId : 0);
        if (targetUserId == 0) return false;

        var checkSql = @"
        SELECT COUNT(*) FROM Reviews
        WHERE AuthorId = @UserId AND TargetUserId = @TargetId AND ProductId = @ProductId";
        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@UserId", userId);
        checkCmd.Parameters.AddWithValue("@TargetId", targetUserId);
        checkCmd.Parameters.AddWithValue("@ProductId", productId);
        var exists = (int)await checkCmd.ExecuteScalarAsync();

        return exists == 0;
    }

    public async Task<int?> GetReviewTargetIdAsync(int conversationId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
        SELECT ProductId, BuyerId, SellerId
        FROM Conversations
        WHERE Id = @ConvId AND IsSystem = 1";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ConvId", conversationId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var productId = reader.GetInt32(0);
        var buyerId = reader.GetInt32(1);
        var sellerId = reader.GetInt32(2);

        int targetUserId = (userId == buyerId) ? sellerId : (userId == sellerId ? buyerId : 0);
        return targetUserId == 0 ? null : targetUserId;
    }

    public async Task<bool> HasUserReviewedAsync(int conversationId, int userId)
    {
        return !await CanReviewAsync(conversationId, userId);
    }

    public async Task<int> CreateReviewAsync(int authorId, int targetUserId, int productId, int rating, string? comment)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
        INSERT INTO Reviews (AuthorId, TargetUserId, Rating, Comment, CreatedAt, ProductId)
        OUTPUT INSERTED.Id
        VALUES (@AuthorId, @TargetId, @Rating, @Comment, GETDATE(), @ProductId)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@AuthorId", authorId);
        cmd.Parameters.AddWithValue("@TargetId", targetUserId);
        cmd.Parameters.AddWithValue("@Rating", rating);
        cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        return (int)await cmd.ExecuteScalarAsync();
    }
    public async Task<List<User>> GetConversationPartnersAsync(int productId, int userId)
    {
        var result = new List<User>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
        SELECT DISTINCT u.Id, u.UserName, u.AvatarUrl, u.IsCompany
        FROM Conversations c
        JOIN Users u ON (u.Id = c.BuyerId OR u.Id = c.SellerId)
        WHERE c.ProductId = @ProductId 
          AND (c.BuyerId = @UserId OR c.SellerId = @UserId)
          AND u.Id != @UserId";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new User
            {
                Id = reader.GetInt32(0),
                UserName = reader.GetString(1),
                AvatarUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsCompany = !reader.IsDBNull(3) && reader.GetBoolean(3)
            });
        }
        return result;
    }

    public async Task<int> CreateSystemConversationAsync(int productId, int user1Id, int user2Id)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
        INSERT INTO Conversations (ProductId, BuyerId, SellerId, CreatedAt, IsSystem)
        OUTPUT INSERTED.Id
        VALUES (@ProductId, @User1Id, @User2Id, GETDATE(), 1)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ProductId", productId);
        cmd.Parameters.AddWithValue("@User1Id", user1Id);
        cmd.Parameters.AddWithValue("@User2Id", user2Id);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<int> SendSystemMessageAsync(int conversationId, string text)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"
        INSERT INTO Messages (ConversationId, SenderId, Text, CreatedAt, IsRead)
        OUTPUT INSERTED.Id
        VALUES (@ConvId, NULL, @Text, GETDATE(), 0)";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ConvId", conversationId);
        cmd.Parameters.AddWithValue("@Text", text);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }
    public async Task<List<Conversation>> GetConversationsAsync(int userId)
    {
        var result = new List<Conversation>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
        WITH user_role AS (
    SELECT Role FROM Users WHERE Id = @UserId
)
SELECT c.Id, c.ProductId, c.BuyerId, c.SellerId, c.IsSystem, c.IsSupport, c.CreatedAt,
       p.Name,
       COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarUrl
       ) AS AvatarUrl,
       CASE 
           WHEN c.IsSupport = 1 AND ur.Role = 'admin' THEN bu.UserName
           WHEN c.IsSupport = 1 AND ur.Role != 'admin' THEN 'Tech Support'
           WHEN c.BuyerId = @UserId THEN su.UserName ELSE bu.UserName 
       END AS OtherUserName,
       CASE 
           WHEN c.IsSupport = 1 AND ur.Role != 'admin' THEN NULL
           WHEN c.BuyerId = @UserId THEN su.AvatarUrl ELSE bu.AvatarUrl 
       END AS OtherUserAvatar,
       CASE 
           WHEN c.IsSupport = 1 AND ur.Role = 'admin' THEN c.BuyerId
           WHEN c.BuyerId = @UserId THEN c.SellerId ELSE c.BuyerId 
       END AS OtherUserId,
       CASE 
            WHEN c.IsSupport = 1 AND ur.Role = 'admin' THEN CAST(0 AS BIT)
            WHEN c.BuyerId = @UserId THEN CAST(IIF(su.Role = 'banned', 1, 0) AS BIT)
            ELSE CAST(IIF(bu.Role = 'banned', 1, 0) AS BIT)
        END AS OtherUserIsBanned,
       (SELECT TOP 1 Text FROM Messages WHERE ConversationId = c.Id ORDER BY CreatedAt DESC) AS LastMessage,
       (SELECT TOP 1 CreatedAt FROM Messages WHERE ConversationId = c.Id ORDER BY CreatedAt DESC) AS LastMessageAt,
       (SELECT COUNT(*) FROM Messages WHERE ConversationId = c.Id AND IsRead = 0 AND SenderId != @UserId AND IsAdmin = 0) AS UnreadCount
FROM Conversations c
CROSS JOIN user_role ur
LEFT JOIN Products p ON p.Id = c.ProductId
LEFT JOIN Users bu ON bu.Id = c.BuyerId
LEFT JOIN Users su ON su.Id = c.SellerId
WHERE (c.BuyerId = @UserId OR c.SellerId = @UserId OR (c.IsSupport = 1 AND ur.Role = 'admin'))
ORDER BY LastMessageAt DESC", conn);

        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new Conversation
            {
                Id = r.GetInt32(0),
                ProductId = r.IsDBNull(1) ? null : r.GetInt32(1),
                BuyerId = r.GetInt32(2),
                SellerId = r.GetInt32(3),
                IsSystem = r.GetBoolean(4),
                IsSupport = r.GetBoolean(5),
                CreatedAt = r.GetDateTime(6),
                ProductName = r.IsDBNull(7) ? null : r.GetString(7),
                ProductImage = r.IsDBNull(8) ? null : r.GetString(8),
                OtherUserName = r.IsDBNull(9) ? null : r.GetString(9),
                OtherUserAvatar = r.IsDBNull(10) ? null : r.GetString(10),
                OtherUserId = r.IsDBNull(11) ? 0 : r.GetInt32(11),
                OtherUserIsBanned = r.IsDBNull(12) ? false : r.GetBoolean(12),
                LastMessage = r.IsDBNull(13) ? null : r.GetString(13),
                LastMessageAt = r.IsDBNull(14) ? null : r.GetDateTime(14),
                UnreadCount = r.IsDBNull(15) ? 0 : r.GetInt32(15),
            });
        }
        return result;
    }
    public async Task<Conversation?> GetConversationAsync(int conversationId, int userId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(@"
        WITH user_role AS (
    SELECT Role FROM Users WHERE Id = @UserId
)
SELECT c.Id, c.ProductId, c.BuyerId, c.SellerId, c.IsSystem, c.IsSupport, c.CreatedAt,
       p.Name,
       COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarUrl
       ) AS AvatarUrl,
       p.Status,
       CASE 
           WHEN c.IsSupport = 1 AND ur.Role = 'admin' THEN bu.UserName
           WHEN c.IsSupport = 1 AND ur.Role != 'admin' THEN 'Tech Support'
           WHEN c.BuyerId = @UserId THEN su.UserName ELSE bu.UserName 
       END AS OtherUserName,
       CASE 
           WHEN c.IsSupport = 1 AND ur.Role != 'admin' THEN NULL
           WHEN c.BuyerId = @UserId THEN su.AvatarUrl ELSE bu.AvatarUrl 
       END AS OtherUserAvatar,
       CASE 
           WHEN c.IsSupport = 1 AND ur.Role = 'admin' THEN c.BuyerId
           WHEN c.BuyerId = @UserId THEN c.SellerId ELSE c.BuyerId 
       END AS OtherUserId,
       CASE 
            WHEN c.IsSupport = 1 AND ur.Role = 'admin' THEN CAST(0 AS BIT)
            WHEN c.BuyerId = @UserId THEN CAST(IIF(su.Role = 'banned', 1, 0) AS BIT)
            ELSE CAST(IIF(bu.Role = 'banned', 1, 0) AS BIT)
        END AS OtherUserIsBanned
FROM Conversations c
CROSS JOIN user_role ur
LEFT JOIN Products p ON p.Id = c.ProductId
LEFT JOIN Users bu ON bu.Id = c.BuyerId
LEFT JOIN Users su ON su.Id = c.SellerId
WHERE c.Id = @Id 
  AND (c.BuyerId = @UserId OR c.SellerId = @UserId OR (c.IsSupport = 1 AND ur.Role = 'admin'))", conn);

        cmd.Parameters.AddWithValue("@Id", conversationId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        string? productStatus = r.IsDBNull(9) ? null : r.GetString(9);

        var conv = new Conversation
        {
            Id = r.GetInt32(0),
            ProductId = r.IsDBNull(1) ? null : r.GetInt32(1),
            BuyerId = r.GetInt32(2),
            SellerId = r.GetInt32(3),
            IsSystem = r.GetBoolean(4),
            IsSupport = r.GetBoolean(5),
            CreatedAt = r.GetDateTime(6),
            ProductName = r.IsDBNull(7) ? null : r.GetString(7),
            ProductImage = r.IsDBNull(8) ? null : r.GetString(8),
            OtherUserName = r.IsDBNull(10) ? null : r.GetString(10),
            OtherUserAvatar = r.IsDBNull(11) ? null : r.GetString(11),
            OtherUserId = r.IsDBNull(12) ? 0 : r.GetInt32(12),
            OtherUserIsBanned = r.IsDBNull(13) ? false : r.GetBoolean(13)
        };

        await r.CloseAsync();

        if (conv.ProductId.HasValue && productStatus == "Succeeded")
        {
            int targetUserId = (userId == conv.BuyerId) ? conv.SellerId : (userId == conv.SellerId ? conv.BuyerId : 0);
            if (targetUserId != 0)
            {
                await using var checkCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Reviews
                WHERE AuthorId = @UserId AND TargetUserId = @TargetId AND ProductId = @ProductId", conn);
                checkCmd.Parameters.AddWithValue("@UserId", userId);
                checkCmd.Parameters.AddWithValue("@TargetId", targetUserId);
                checkCmd.Parameters.AddWithValue("@ProductId", conv.ProductId.Value);
                int exists = (int)await checkCmd.ExecuteScalarAsync();

                conv.CanReview = exists == 0;
                if (conv.CanReview)
                {
                    conv.ReviewTargetId = targetUserId;
                    conv.ProductIdForReview = conv.ProductId;
                }
            }
        }
        else
        {
            conv.CanReview = false;
        }

        return conv;
    }
    public async Task<int> GetOrCreateConversationAsync(int productId, int buyerId, int sellerId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var findCmd = new SqlCommand(@"
                SELECT Id FROM Conversations
                WHERE ProductId = @ProductId AND BuyerId = @BuyerId AND SellerId = @SellerId", conn);
        findCmd.Parameters.AddWithValue("@ProductId", productId);
        findCmd.Parameters.AddWithValue("@BuyerId", buyerId);
        findCmd.Parameters.AddWithValue("@SellerId", sellerId);
        var existing = await findCmd.ExecuteScalarAsync();
        if (existing != null) return (int)existing;

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
                SELECT m.Id, m.ConversationId, m.SenderId, m.Text, m.CreatedAt, m.IsRead, m.IsAdmin,
                       u.UserName, u.AvatarUrl
                FROM Messages m
                LEFT JOIN Users u ON u.Id = m.SenderId
                WHERE m.ConversationId = @ConvId
                ORDER BY m.CreatedAt ASC", conn);
        cmd.Parameters.AddWithValue("@ConvId", conversationId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            result.Add(new Message
            {
                Id = r.GetInt32(0),
                ConversationId = r.GetInt32(1),
                SenderId = r.IsDBNull(2) ? null : r.GetInt32(2),
                Text = r.GetString(3),
                CreatedAt = r.GetDateTime(4),
                IsRead = r.GetBoolean(5),
                IsAdmin = r.IsDBNull(6) ? false : r.GetBoolean(6),
                SenderName = r.IsDBNull(7) ? null : r.GetString(7),
                SenderAvatar = r.IsDBNull(8) ? null : r.GetString(8),
            });
        return result;
    }

    public async Task<int> SendMessageAsync(int conversationId, int senderId, string text)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(@"
                INSERT INTO Messages (ConversationId, SenderId, Text, CreatedAt, IsRead)
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
