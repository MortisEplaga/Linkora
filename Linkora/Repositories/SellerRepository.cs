using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class SellerRepository : ISellerRepository
    {
        private readonly string _connectionString;

        public SellerRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<UserSummary?> GetByIdAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, AvatarUrl, Phone, Email, IsCompany, CreatedAt FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            return new UserSummary
            {
                Id = r.GetInt32(0),
                UserName = r.IsDBNull(1) ? null : r.GetString(1),
                AvatarUrl = r.IsDBNull(2) ? null : r.GetString(2),
                Phone = r.IsDBNull(3) ? null : r.GetString(3),
                Email = r.IsDBNull(4) ? null : r.GetString(4),
                IsCompany = !r.IsDBNull(5) && r.GetBoolean(5),
                CreatedAt = r.IsDBNull(6) ? null : r.GetDateTime(6),
            };
        }
        public async Task<(int Count, double Avg)> GetRatingAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(*), AVG(CAST(Rating AS float)) FROM Reviews WHERE TargetUserId = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", userId);
            await using var r = await cmd.ExecuteReaderAsync();

            if (await r.ReadAsync() && !r.IsDBNull(0) && r.GetInt32(0) > 0)
                return (r.GetInt32(0), r.GetDouble(1));

            return (0, 0.0);
        }
        public async Task<List<CategoryCount>> GetCategoriesAsync(int userId, string lang)
        {
            var result = new List<CategoryCount>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT DISTINCT c.Id, c.Name, c.NameLV, c.NameRU, COUNT(p.Id) as Cnt
                FROM Products p
                JOIN Category c ON c.Id = p.CategoryId
                WHERE p.UserId = @UserId
                  AND (p.Status = 'active' OR p.Status IS NULL)
                GROUP BY c.Id, c.Name, c.NameLV, c.NameRU
                ORDER BY Cnt DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var catId = r.GetInt32(0);
                var nameEn = r.GetString(1);
                var nameLv = r.IsDBNull(2) ? null : r.GetString(2);
                var nameRu = r.IsDBNull(3) ? null : r.GetString(3);
                var count = r.GetInt32(4);

                var localizedName = lang switch
                {
                    "lv" => nameLv ?? nameEn,
                    "ru" => nameRu ?? nameEn,
                    _ => nameEn
                };

                result.Add(new CategoryCount
                {
                    Id = catId,
                    Name = localizedName,
                    Count = count
                });
            }

            return result;
        }
        public async Task<List<Product>> GetProductsAsync(int userId, int? categoryId, string sort)
        {
            var result = new List<Product>();

            var order = sort switch
            {
                "cheap" => @"(SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                                  FROM MapperProductCategory m
                                  JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                                  WHERE m.ProductId = p.Id) ASC",
                "expensive" => @"(SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                                  FROM MapperProductCategory m
                                  JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                                  WHERE m.ProductId = p.Id) DESC",
                _ => "p.CreatedAt DESC"
            };

            var catFilter = categoryId.HasValue ? "AND p.CategoryId = @CatId" : "";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
                SELECT p.Id, p.Name, p.Address, p.CreatedAt, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarUrl
       ) AS AvatarUrl,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c2 ON c2.Id = m.CategoryId AND c2.Name = 'Price, €'
                        WHERE m.ProductId = p.Id) as Price
                FROM Products p
                WHERE p.UserId = @UserId
                  AND (p.Status = 'active' OR p.Status IS NULL)
                  {catFilter}
                ORDER BY {order}", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            if (categoryId.HasValue)
                cmd.Parameters.AddWithValue("@CatId", categoryId.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarUrl = r.IsDBNull(4) ? null : r.GetString(4),
                    Price = r.IsDBNull(5) ? null : r.GetDecimal(5),
                });

            return result;
        }
        public async Task<List<dynamic>> GetReviewsAsync(int userId, int limit = 50)
        {
            var result = new List<dynamic>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT r.Id, r.Rating, r.Comment, r.CreatedAt,
                       u.UserName, u.AvatarUrl
                FROM Reviews r
                JOIN Users u ON u.Id = r.AuthorId
                WHERE r.TargetUserId = @Id
                ORDER BY r.CreatedAt DESC
                OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY", conn);
            cmd.Parameters.AddWithValue("@Id", userId);
            cmd.Parameters.AddWithValue("@Limit", limit);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new
                {
                    Id = r.GetInt32(0),
                    Rating = r.GetInt32(1),
                    Comment = r.IsDBNull(2) ? "" : r.GetString(2),
                    CreatedAt = r.GetDateTime(3),
                    AuthorName = r.IsDBNull(4) ? "Unknown" : r.GetString(4),
                    AuthorAvatar = r.IsDBNull(5) ? null : r.GetString(5),
                });

            return result;
        }
    }
}