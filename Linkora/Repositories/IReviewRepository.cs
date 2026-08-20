using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IReviewRepository
    {
        Task<List<ReviewRow>> GetUserReviewsAsync(int userId, string tab);
    }
}