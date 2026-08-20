using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IPaymentRepository
    {
        Task<int> CreateAsync(int userId, string purpose, int? productId, string? promotionType,
            string? subscriptionType, decimal amount, string reference);
        Task SetTransactionIdAsync(int paymentId, string transactionId);
        Task SetStatusAsync(int paymentId, string status);
        Task<PaymentRecord?> GetByReferenceAsync(string reference);
        Task MarkCompletedAsync(int paymentId);
        Task ApplyPromotionAsync(int productId, string promotionType);
        Task ApplySubscriptionAsync(int userId, string subscriptionType);
        Task<int?> GetProductOwnerIdAsync(int productId);
    }
}