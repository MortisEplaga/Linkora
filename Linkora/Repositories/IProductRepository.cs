using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8);
        Task<List<Product>> GetByUserAsync(int userId, string status = "active");
        Task UpdateAsync(Product product);
        Task<Dictionary<int, string>> GetParamValuesAsync(int productId);
        Task<List<Product>> GetByCategoryAsync(
                                                IEnumerable<int> categoryIds,
                                                string sort = "new",
                                                Dictionary<int, List<string>>? filters = null,
                                                Dictionary<int, decimal>? rangeFrom = null,
                                                Dictionary<int, decimal>? rangeTo = null,
                                                int? priceParamId = null,
                                                string? city = null);    

    }
}
