using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IFavouriteRepository
    {
        Task<bool> ToggleAsync(int productId, int userId, bool can);
        Task<(List<int> Favs, List<int> Cart)> GetUserItemIdsAsync(int userId);
        Task<(List<Product> Favs, List<Product> Cart)> GetUserItemsAsync(int userId);
    }
}