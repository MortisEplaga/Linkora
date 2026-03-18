using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8);
        Task<List<Product>> GetByUserAsync(int userId, string status = "active");
        Task UpdateAsync(Product product, Dictionary<int, string> paramValues);
        Task<Dictionary<int, string>> GetParamValuesAsync(int productId);
        Task<Dictionary<string, int>> GetCountsByStatusAsync(int userId);
        Task<List<Product>> GetByCategoryAsync(
                                                IEnumerable<int> categoryIds,
                                                string sort = "new",
                                                Dictionary<int, List<string>>? filters = null,
                                                Dictionary<int, decimal>? rangeFrom = null,
                                                Dictionary<int, decimal>? rangeTo = null,
                                                int? priceParamId = null,
                                                string? city = null);
        Task<bool> CompleteDealAsync(int productId, int userId);
        Task<bool> ReactivateProductAsync(int productId, int userId);
        Task<IEnumerable<Product>> GetUserProductsByStatusAsync(int userId, string status);
        Task<bool> UpdateProductStatusAsync(int productId, ProductStatus status);
        Task<int> CreateAsync(Product product, Dictionary<int, string> paramValues);
        Task<List<ProductMedia>> GetMediaAsync(int productId);
        Task SaveMediaAsync(int productId, List<ProductMedia> media);
        Task DeleteMediaAsync(int productId);
        Task IncrementViewCountAsync(int productId);

    }
}
