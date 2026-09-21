using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IPointsRepository
    {
        Task<int> GetBalanceAsync(int userId);
        Task<bool> SpendOnSubscriptionAsync(int userId, PromotionTier tier, PromotionTermType term, decimal eurPrice, int pointsCost, DateTime startedAt, DateTime expiresAt, int? supersedeId);
        Task<bool> SpendOnListingBoostAsync(int userId, int productId, PromotionTier tier, DateTime expiresAt, int pointsCost);
    }
}