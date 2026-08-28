using Linkora.Models;

namespace Linkora.Repositories
{
    public class FavouriteRepository : SqlRepositoryBase, IFavouriteRepository
    {
        public FavouriteRepository(IConfiguration config) : base(config) { }
        public async Task<bool> ToggleAsync(int productId, int userId, bool can)
        {
            var existingIds = await QueryAsync(
                "SELECT TOP 1 Id FROM Favourites WHERE ProductId = @P AND UserId = @U AND Can = @C",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@P", productId);
                    p.AddWithValue("@U", userId);
                    p.AddWithValue("@C", can);
                });

            var existingId = existingIds.FirstOrDefault();
            if (existingId > 0)
            {
                await ExecuteAsync(
                    "DELETE FROM Favourites WHERE Id = @Id",
                    p => p.AddWithValue("@Id", existingId));
                return false;
            }

            await ExecuteAsync(
                "INSERT INTO Favourites (ProductId, UserId, Can) VALUES (@P, @U, @C)",
                p =>
                {
                    p.AddWithValue("@P", productId);
                    p.AddWithValue("@U", userId);
                    p.AddWithValue("@C", can);
                });
            return true;
        }
        public async Task<(List<int> Favs, List<int> Cart)> GetUserItemIdsAsync(int userId)
        {
            var items = await QueryAsync(
                "SELECT ProductId, Can FROM Favourites WHERE UserId = @U",
                r => (ProductId: r.GetInt32(0), Can: r.GetBoolean(1)),
                p => p.AddWithValue("@U", userId));

            var favs = items.Where(x => x.Can).Select(x => x.ProductId).ToList();
            var cart = items.Where(x => !x.Can).Select(x => x.ProductId).ToList();

            return (favs, cart);
        }
        public async Task<(List<Product> Favs, List<Product> Cart)> GetUserItemsAsync(int userId)
        {
            var items = await QueryAsync(
                @"SELECT f.Can, p.Id, p.Name,
                 (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                  FROM MapperProductCategory m
                  JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                  WHERE m.ProductId = p.Id) as Price,
                 p.Address, p.CreatedAt,
                 COALESCE(
                 (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
                  WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                 p.AvatarUrl
             ) AS AvatarUrl, u.UserName, u.AvatarUrl, u.IsCompany,
             u.Phone, u.Email, u.CreatedAt AS SellerCreatedAt, u.TelegramUrl, u.WhatsAppUrl, u.WebsiteUrl
          FROM Favourites f
          JOIN Products p ON p.Id = f.ProductId
          LEFT JOIN Users u ON u.Id = p.UserId
          WHERE f.UserId = @U",
                r => (
                    Can: r.GetBoolean(0),
                    Product: new Product
                    {
                        Id = r.GetInt32(1),
                        Name = r.GetStringOrDefault(2),
                        Price = r.GetDecimalOrNull(3),
                        Address = r.GetStringOrNull(4),
                        CreatedAt = r.GetDateTimeOrNull(5),
                        AvatarUrl = r.GetStringOrNull(6),
                        Seller = new UserSummary
                        {
                            UserName = r.GetStringOrNull(7),
                            AvatarUrl = r.GetStringOrNull(8),
                            IsCompany = r.GetBooleanOrDefault(9),
                            Phone = r.GetStringOrNull(10),
                            Email = r.GetStringOrNull(11),
                            CreatedAt = r.GetDateTimeOrNull(12),
                            TelegramUrl = r.GetStringOrNull(13),
                            WhatsAppUrl = r.GetStringOrNull(14),
                            WebsiteUrl = r.GetStringOrNull(15)
                        }
                    }
                ),
                p => p.AddWithValue("@U", userId));

            var favs = items.Where(x => x.Can).Select(x => x.Product).ToList();
            var cart = items.Where(x => !x.Can).Select(x => x.Product).ToList();

            return (favs, cart);
        }
    }
}