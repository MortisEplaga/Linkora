using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ISellerRepository
    {
        Task<SellerViewModel?> GetByIdAsync(int id);
        Task<(int Count, double Avg)> GetRatingAsync(int userId);
        Task<List<CategoryCount>> GetCategoriesAsync(int userId, string lang);
        Task<List<Product>> GetProductsAsync(int userId, int? categoryId, string sort);
        Task<List<dynamic>> GetReviewsAsync(int userId, int limit = 50);
    }
}