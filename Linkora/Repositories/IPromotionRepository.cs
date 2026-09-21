using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IPromotionRepository
    {
        Task<Promotion?> GetActiveAsync(int userId);
        Task<int> CreateAsync(int userId, PromotionTier tier, PromotionTermType termType, DateTime startedAt, DateTime expiresAt, decimal pricePaid, int? paymentId = null);
        Task SupersedeAsync(int promotionId);
        Task<int> ExpireDueAsync();
    }
}