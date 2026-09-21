using Linkora.Models;

namespace Linkora.Repositories
{
    public class PaymentRepository : SqlRepositoryBase, IPaymentRepository
    {
        public PaymentRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<int> CreateAsync(int userId, string purpose, int? productId, string? promotionTier, string? subscriptionTier, decimal price, string reference, int pointsSpent) => (await QueryAsync<int>(
                @"INSERT INTO Payments (UserId, PurposeType, ProductId, PromotionTier, SubscriptionTier, Price, Currency, Reference, Status, PointsSpent, CreatedAt)
                  OUTPUT INSERTED.Id
                  VALUES (@UserId, @Purpose, @ProductId, @PromotionTier, @SubscriptionTier, @Price, 'EUR', @Reference, 'Created', @PointsSpent, SYSUTCDATETIME())",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Purpose", purpose);
                    p.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);
                    p.AddWithValue("@PromotionTier", (object?)promotionTier ?? DBNull.Value);
                    p.AddWithValue("@SubscriptionTier", (object?)subscriptionTier ?? DBNull.Value);
                    p.AddWithValue("@Price", price);
                    p.AddWithValue("@Reference", reference);
                    p.AddWithValue("@PointsSpent", pointsSpent);
                }))[0];
        public async Task SetTransactionIdAsync(int paymentId, string transactionId) => await ExecuteAsync(
                "UPDATE Payments SET TransactionId = @TxId, Status = 'Pending' WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@TxId", transactionId);
                    p.AddWithValue("@Id", paymentId);
                });
        public async Task SetStatusAsync(int paymentId, string status) => await ExecuteAsync(
                "UPDATE Payments SET Status = @Status WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Status", status);
                    p.AddWithValue("@Id", paymentId);
                });
        public async Task<PaymentBase?> GetByReferenceAsync(string reference) => await QuerySingleAsync(
                "SELECT Id, Status, PurposeType, ProductId, PromotionTier, SubscriptionTier, UserId, PointsSpent, Price FROM Payments WHERE Reference = @Reference",
                r => new PaymentBase
                {
                    Id = r.GetInt32(0),
                    Status = r.GetString(1),
                    PurposeType = r.GetString(2),
                    ProductId = r.GetInt32OrNull(3),
                    PromotionTier = r.GetStringOrNull(4),
                    SubscriptionTier = r.GetStringOrNull(5),
                    UserId = r.GetInt32(6),
                    PointsSpent = r.GetInt32OrDefault(7),
                    Price = r.GetDecimal(8),
                },
                p => p.AddWithValue("@Reference", reference));
        public async Task MarkCompletedAsync(int paymentId) => await ExecuteAsync("UPDATE Payments SET Status = 'Completed', CompletedAt = SYSUTCDATETIME() WHERE Id = @Id", p => p.AddWithValue("@Id", paymentId));
        public async Task ApplyPromotionAsync(int productId, PromotionTier tier, DateTime expiresAt) => await ExecuteAsync("UPDATE Products SET PaidBoostLevel = @Tier, PaidBoostExpiresAt = @Exp, PaidBoostPoints = NULL WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Tier", (short)tier);
                    p.AddWithValue("@Exp", expiresAt);
                    p.AddWithValue("@Id", productId);
                });
        public async Task<int?> GetProductUserIdAsync(int productId)
        {
            var result = await QueryAsync<int?>(
                "SELECT UserId FROM Products WHERE Id = @Id",
                r => r.GetInt32OrNull(0),
                p => p.AddWithValue("@Id", productId));

            return result.Count > 0 ? result[0] : null;
        }
    }
}