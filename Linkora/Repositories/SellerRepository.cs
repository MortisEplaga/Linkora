using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class SellerRepository : SqlRepositoryBase, ISellerRepository
    {
        public SellerRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<UserSummary?> GetByIdAsync(int id) => await QuerySingleAsync(
                "SELECT Id, UserName, AvatarUrl, Phone, Email, IsCompany, CreatedAt, TelegramUrl, WhatsAppUrl, WebsiteUrl FROM Users WHERE Id = @Id",
                r => new UserSummary
                {
                    Id = r.GetInt32(0),
                    UserName = r.GetStringOrNull(1),
                    AvatarUrl = r.GetStringOrNull(2),
                    Phone = r.GetStringOrNull(3),
                    Email = r.GetStringOrNull(4),
                    IsCompany = r.GetBooleanOrDefault(5),
                    CreatedAt = r.GetDateTimeOrNull(6),
                    TelegramUrl = r.GetStringOrNull(7),
                    WhatsAppUrl = r.GetStringOrNull(8),
                    WebsiteUrl = r.GetStringOrNull(9)
                }, p => p.AddWithValue("@Id", id));
        public async Task<(int Count, double Avg)> GetRatingAsync(int userId)
        {
            var result = await QueryAsync<(int Count, double Avg)>(
                "SELECT COUNT(*), AVG(CAST(Rating AS float)) FROM Reviews WHERE TargetUserId = @Id",
                r => (
                    r.GetInt32(0),
                    r.GetDoubleOrDefault(1)
                ), p => p.AddWithValue("@Id", userId));

            return result.Count > 0 ? result[0] : (0, 0.0);
        }
        public async Task<List<CategoryCount>> GetCategoriesAsync(int userId, string lang) => await QueryAsync(
                @"SELECT DISTINCT c.Id, c.Name, c.NameLV, c.NameRU, COUNT(p.Id) as Cnt
                  FROM Products p
                  JOIN Category c ON c.Id = p.CategoryId
                  WHERE p.UserId = @UserId
                    AND (p.Status = 'active' OR p.Status IS NULL)
                  GROUP BY c.Id, c.Name, c.NameLV, c.NameRU
                  ORDER BY Cnt DESC",
                r => new CategoryCount
                {
                    Id = r.GetInt32(0),
                    Name = Resolve(lang, r.GetString(1), r.GetStringOrNull(2), r.GetStringOrNull(3)),
                    Count = r.GetInt32(4)
                }, p => p.AddWithValue("@UserId", userId));
        public async Task<PagedResult<Product>> GetProductsPagedAsync(int userId, int? categoryId, string sort, int page)
        {
            if (page < 1) page = 1;
            int offset = (page - 1) * 20;

            string orderBy = sort switch
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

            string catFilter = categoryId.HasValue ? "AND p.CategoryId = @CategoryId" : "";

            var countSql = $@"SELECT COUNT(*) FROM Products p WHERE p.UserId = @UserId AND (p.Status = 'active' OR p.Status IS NULL) {catFilter}";
            var countParams = new List<SqlParameter> { new SqlParameter("@UserId", userId) };
            if (categoryId.HasValue) countParams.Add(new SqlParameter("@CategoryId", categoryId.Value));

            var totalItems = (await QueryAsync(countSql, r => r.GetInt32(0), p => { foreach (var sp in countParams) p.Add(sp); })).FirstOrDefault();

            var dataSql = $@"SELECT p.Id, p.Name, p.Address, p.CreatedAt, 
                                    COALESCE(
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
                             ORDER BY {orderBy}
                             OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var items = await QueryAsync(dataSql, r => new Product
            {
                Id = r.GetInt32(0),
                Name = r.GetStringOrDefault(1),
                Address = r.GetStringOrNull(2),
                CreatedAt = r.GetDateTimeOrNull(3),
                AvatarUrl = r.GetStringOrNull(4),
                Price = r.GetDecimalOrNull(5)
            }, p =>
            {
                p.AddWithValue("@UserId", userId);
                if (categoryId.HasValue) p.AddWithValue("@CategoryId", categoryId.Value);
                p.AddWithValue("@Offset", offset);
                p.AddWithValue("@PageSize", 20);
            });

            return new PagedResult<Product>
            {
                Items = items,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)20),
                Total = totalItems
            };
        }
        public async Task<List<dynamic>> GetReviewsAsync(int userId, int limit = 50) => await QueryAsync<dynamic>(
                @"SELECT r.Id, r.Rating, r.Comment, r.CreatedAt,
                         u.UserName, u.AvatarUrl
                  FROM Reviews r
                  JOIN Users u ON u.Id = r.AuthorId
                  WHERE r.TargetUserId = @Id
                  ORDER BY r.CreatedAt DESC
                  OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY",
                r => new
                {
                    Id = r.GetInt32(0),
                    Rating = r.GetInt32(1),
                    Comment = r.GetStringOrDefault(2),
                    CreatedAt = r.GetDateTime(3),
                    AuthorName = r.GetStringOrDefault(4, "Unknown"),
                    AuthorAvatar = r.GetStringOrNull(5),
                },
                p =>
                {
                    p.AddWithValue("@Id", userId);
                    p.AddWithValue("@Limit", limit);
                });
    }
}