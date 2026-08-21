using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IReviewRepository
    {
        Task<List<ReviewRow>> GetUserReviewsAsync(int userId, string tab);
        Task<bool> CanReviewAsync(int authorId, int targetUserId, int productId);
    }
}