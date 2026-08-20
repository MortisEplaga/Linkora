using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly string _connectionString;

        public PaymentRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<int> CreateAsync(int userId, string purpose, int? productId, string? promotionType,
            string? subscriptionType, decimal amount, string reference)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Payments (UserId, PurposeType, ProductId, PromotionType, SubscriptionType, Amount, Currency, Reference, Status, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@UserId, @Purpose, @ProductId, @PromotionType, @SubscriptionType, @Amount, 'EUR', @Reference, 'Created', SYSUTCDATETIME())", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Purpose", purpose);
            cmd.Parameters.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PromotionType", (object?)promotionType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SubscriptionType", (object?)subscriptionType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Reference", reference);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        public async Task SetTransactionIdAsync(int paymentId, string transactionId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Payments SET TransactionId = @TxId, Status = 'Pending' WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@TxId", transactionId);
            cmd.Parameters.AddWithValue("@Id", paymentId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task SetStatusAsync(int paymentId, string status)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("UPDATE Payments SET Status = @Status WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@Id", paymentId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<PaymentRecord?> GetByReferenceAsync(string reference)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, Status, PurposeType, ProductId, PromotionType, SubscriptionType, UserId FROM Payments WHERE Reference = @Reference", conn);
            cmd.Parameters.AddWithValue("@Reference", reference);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new PaymentRecord
            {
                Id = r.GetInt32(0),
                Status = r.GetString(1),
                Purpose = r.GetString(2),
                ProductId = r.IsDBNull(3) ? null : r.GetInt32(3),
                PromotionType = r.IsDBNull(4) ? null : r.GetString(4),
                SubscriptionType = r.IsDBNull(5) ? null : r.GetString(5),
                UserId = r.GetInt32(6),
            };
        }
        public async Task MarkCompletedAsync(int paymentId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Payments SET Status = 'Completed', CompletedAt = SYSUTCDATETIME() WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", paymentId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task ApplyPromotionAsync(int productId, string promotionType)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Products SET PromotionType = @Type WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Type", promotionType);
            cmd.Parameters.AddWithValue("@Id", productId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task ApplySubscriptionAsync(int userId, string subscriptionType)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET SubscriptionType = @Type WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Type", subscriptionType);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<int?> GetProductOwnerIdAsync(int productId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT UserId FROM Products WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", productId);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : (int)result;
        }
    }
}