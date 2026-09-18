using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IPaymentRepository
    {
        Task<int> CreateAsync(int userId, string purpose, int? productId, string? promotionType,
            string? subscriptionType, decimal price, string reference, int pointsSpent);
        Task SetTransactionIdAsync(int paymentId, string transactionId);
        Task SetStatusAsync(int paymentId, string status);
        Task<PaymentBase?> GetByReferenceAsync(string reference);
        Task MarkCompletedAsync(int paymentId);
        Task ApplyPromotionAsync(int productId, PromotionTier tier, DateTime expiresAt);
        Task<int?> GetProductUserIdAsync(int productId);
    }
}