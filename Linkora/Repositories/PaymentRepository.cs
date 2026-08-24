using Linkora.Models;

namespace Linkora.Repositories
{
    public class PaymentRepository : SqlRepositoryBase, IPaymentRepository
    {
        public PaymentRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<int> CreateAsync(int userId, string purpose, int? productId, string? promotionType, 
                                           string? subscriptionType, decimal price, string reference) => (await QueryAsync<int>(
                @"INSERT INTO Payments (UserId, PurposeType, ProductId, PromotionType, SubscriptionType, Price, Currency, Reference, Status, CreatedAt)
                  OUTPUT INSERTED.Id
                  VALUES (@UserId, @Purpose, @ProductId, @PromotionType, @SubscriptionType, @Price, 'EUR', @Reference, 'Created', SYSUTCDATETIME())",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Purpose", purpose);
                    p.AddWithValue("@ProductId", (object?)productId ?? DBNull.Value);
                    p.AddWithValue("@PromotionType", (object?)promotionType ?? DBNull.Value);
                    p.AddWithValue("@SubscriptionType", (object?)subscriptionType ?? DBNull.Value);
                    p.AddWithValue("@Price", price);
                    p.AddWithValue("@Reference", reference);
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
                "SELECT Id, Status, PurposeType, ProductId, PromotionType, SubscriptionType, UserId FROM Payments WHERE Reference = @Reference",
                r => new PaymentBase
                {
                    Id = r.GetInt32(0),
                    Status = r.GetString(1),
                    PurposeType = r.GetString(2),
                    ProductId = r.GetInt32OrNull(3),
                    PromotionType = r.GetStringOrNull(4),
                    SubscriptionType = r.GetStringOrNull(5),
                    UserId = r.GetInt32(6),
                },
                p => p.AddWithValue("@Reference", reference));
        public async Task MarkCompletedAsync(int paymentId) => await ExecuteAsync(
                "UPDATE Payments SET Status = 'Completed', CompletedAt = SYSUTCDATETIME() WHERE Id = @Id",
                p => p.AddWithValue("@Id", paymentId));
        public async Task ApplyPromotionAsync(int productId, string promotionType) => await ExecuteAsync(
                "UPDATE Products SET PromotionType = @Type WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Type", promotionType);
                    p.AddWithValue("@Id", productId);
                });
        public async Task ApplySubscriptionAsync(int userId, string subscriptionType) => await ExecuteAsync(
                "UPDATE Users SET SubscriptionType = @Type WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Type", subscriptionType);
                    p.AddWithValue("@Id", userId);
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