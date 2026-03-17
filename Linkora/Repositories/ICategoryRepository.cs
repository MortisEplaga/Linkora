using Linkora.Models;

namespace Linkora.Repositories
{
    public interface ICategoryRepository
    {
        // Все категории (для построения дерева)
        Task<List<Category>> GetAllAsync();

        // Одна категория по Id
        Task<Category?> GetByIdAsync(int id);

        // Дочерние категории
        Task<List<Category>> GetChildrenAsync(int parentId);       // только подкатегории (Type = 1 / null)
        Task<List<Parameter>> GetParametersAsync(int categoryId); // параметры (Type 2–5)

        // Цепочка от корня до нужной категории (для хлебных крошек)
        Task<List<Category>> GetBreadcrumbAsync(int categoryId);
        Task<List<Parameter>> GetParametersAsync(IEnumerable<int> categoryIds);
        Task<List<int>> GetDescendantIdsAsync(int categoryId);
    }
}
