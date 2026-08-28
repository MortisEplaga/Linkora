using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id);
        Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8);
        Task<List<Product>> GetByUserAsync(int userId, string status = "active");
        Task UpdateAsync(Product product, Dictionary<int, string> paramValues, string promotionType = "None");
        Task<Dictionary<int, string>> GetParamValuesAsync(int productId);
        Task<Dictionary<string, int>> GetCountsByStatusAsync(int userId);
        Task<PagedResult<Product>> GetByCategoryAsync(int rootCategoryId, bool includeDescendants = true, string sort = "new", Dictionary<int, List<string>>? filters = null, 
                                               Dictionary<int, decimal>? rangeFrom = null, Dictionary<int, decimal>? rangeTo = null,
                                               int? priceParamId = null, string? city = null, string? search = null, int page = 1); 
        Task<bool> CompleteDealAsync(int productId, int sellerId, int buyerId);
        Task<bool> ReactivateProductAsync(int productId, int userId);
        Task ArchiveProductsByUserAsync(int userId);
        Task<IEnumerable<Product>> GetUserProductsByStatusAsync(int userId, string status);
        Task<bool> UpdateProductStatusAsync(int productId, ProductStatus status);
        Task<int> CreateAsync(Product product, Dictionary<int, string> paramValues, int publishDurationDays = 30, string promotionType = "None");
        Task<List<ProductMedia>> GetMediaAsync(int productId);
        Task SaveMediaAsync(int productId, List<ProductMedia> media);
        Task DeleteMediaAsync(int productId);
        Task IncrementViewCountAsync(int productId);
        Task DeleteAsync(int productId);
        Task<Dictionary<int, string>> GetParamDisplayValuesAsync(int productId, string lang);
        Task<CategoryRulesDto> GetCategoryRulesAsync(IEnumerable<int> categoryIds);
        Task<(List<AdminConfOptionRow> Items, int TotalCount)> GetUnconfirmedOptionsAsync();
        Task<bool> ApproveSelectOptionAsync(int optionId);
        Task<bool> RejectProductAndOptionAsync(int optionId, int productId);
        Task<int?> GetPriceParamIdAsync(int productId);
        Task<int> RecalculateModerationScoreAsync(int productId);
        Task<List<int>> GetFavouriteSubscriberIdsAsync(int productId, int excludeUserId);
        Task<List<Product>> GetPurchasedByUserAsync(int userId);
        Task<int> GetPurchasedConversationCountAsync(int userId);
        Task DeleteSpecificMediaAsync(IEnumerable<int> mediaIds);
        Task UpdatePublishDurationAsync(int productId, int userId, int days);
        Task<List<int>> GetSubscriberIdsExcludingAsync(int sellerId, int excludeBuyerId);
        Task<int> ArchiveExpiredProductsAsync();
        Task<int> ProcessMediaDeletionQueueAsync();

    }
}