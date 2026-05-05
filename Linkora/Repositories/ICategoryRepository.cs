using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();

        Task<Category?> GetByIdAsync(int id);

        Task<List<Category>> GetChildrenAsync(int parentId);       // только подкатегории (Type = 1 / null)
        Task<List<Parameter>> GetParametersAsync(int categoryId); // параметры (Type 2–5)

        Task<List<Category>> GetBreadcrumbAsync(int categoryId);
        Task<List<Parameter>> GetParametersAsync(IEnumerable<int> categoryIds);
        Task<List<int>> GetDescendantIdsAsync(int categoryId);
    }
}
