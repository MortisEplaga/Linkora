using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();
        Task<Category?> GetByIdAsync(int id);
        Task<List<Category>> GetChildrenAsync(int parentId);
        Task<List<Category>> GetBreadcrumbAsync(int rootCategoryId, bool includeSelf = false);
        Task<List<Parameter>> GetParametersAsync(int categoryId, bool forFilter = false);
        Task<List<Parameter>> GetParametersAsync(IEnumerable<int> categoryIds, bool forFilter = false);
        Task RebuildClosureAsync();
    }
}