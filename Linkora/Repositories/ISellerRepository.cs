using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ISellerRepository
    {
        Task<UserSummary?> GetByIdAsync(int id);
        Task<(int Count, double Avg)> GetRatingAsync(int userId);
        Task<List<CategoryCount>> GetCategoriesAsync(int userId, string lang);
        Task<PagedResult<Product>> GetProductsPagedAsync(int userId, int? categoryId, string sort, int page);
        Task<List<dynamic>> GetReviewsAsync(int userId, int limit = 50);
    }
}