using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<Product>> GetByCategoryAsync(
            IEnumerable<int> categoryIds,
            string sort = "new",
            Dictionary<int, List<string>>? filters = null,
            Dictionary<int, decimal>? rangeFrom = null,
            Dictionary<int, decimal>? rangeTo = null,
            int? priceParamId = null,
            string? city = null)
        {
            var ids = string.Join(",", categoryIds);
            if (string.IsNullOrEmpty(ids)) return new List<Product>();

            var priceJoin = priceParamId.HasValue
                ? $"LEFT JOIN MapperProductCategory mpc ON mpc.ProductId = p.Id AND mpc.CategoryId = {priceParamId}"
                : "";
            var priceSelect = priceParamId.HasValue
                ? ", TRY_CAST(mpc.Value AS decimal(18,2)) as Price"
                : @", (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
           FROM MapperProductCategory m
           JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
           WHERE m.ProductId = p.Id) as Price";
            var order = sort switch
            {
                "cheap" => priceParamId.HasValue ? "TRY_CAST(mpc.Value AS decimal(18,2)) ASC" : "p.CreatedTime DESC",
                "expensive" => priceParamId.HasValue ? "TRY_CAST(mpc.Value AS decimal(18,2)) DESC" : "p.CreatedTime DESC",
                _ => "p.CreatedTime DESC"
            };

            var whereClauses = new List<string>();
            var sqlParams = new List<SqlParameter>();
            int pIdx = 0;

            if (filters != null)
            {
                foreach (var (paramId, values) in filters)
                {
                    if (values == null || values.Count == 0) continue;
                    var inParams = values.Select((_, i) => $"@fv{pIdx}_{i}").ToList();
                    whereClauses.Add($@"EXISTS (
                        SELECT 1 FROM MapperProductCategory m
                        WHERE m.ProductId = p.Id AND m.CategoryId = @fp{pIdx}
                        AND m.Value IN ({string.Join(",", inParams)})
                    )");
                    sqlParams.Add(new SqlParameter($"@fp{pIdx}", paramId));
                    for (int i = 0; i < values.Count; i++)
                        sqlParams.Add(new SqlParameter($"@fv{pIdx}_{i}", values[i]));
                    pIdx++;
                }
            }

            if (rangeFrom != null || rangeTo != null)
            {
                var rangeIds = new HashSet<int>();
                if (rangeFrom != null) foreach (var k in rangeFrom.Keys) rangeIds.Add(k);
                if (rangeTo != null) foreach (var k in rangeTo.Keys) rangeIds.Add(k);

                foreach (var paramId in rangeIds)
                {
                    var conditions = new List<string>();
                    sqlParams.Add(new SqlParameter($"@rp{pIdx}", paramId));

                    if (rangeFrom != null && rangeFrom.TryGetValue(paramId, out var from))
                    {
                        conditions.Add($"TRY_CAST(m.Value AS decimal(18,2)) >= @rf{pIdx}");
                        sqlParams.Add(new SqlParameter($"@rf{pIdx}", from));
                    }
                    if (rangeTo != null && rangeTo.TryGetValue(paramId, out var to))
                    {
                        conditions.Add($"TRY_CAST(m.Value AS decimal(18,2)) <= @rt{pIdx}");
                        sqlParams.Add(new SqlParameter($"@rt{pIdx}", to));
                    }

                    whereClauses.Add($@"EXISTS (
                        SELECT 1 FROM MapperProductCategory m
                        WHERE m.ProductId = p.Id AND m.CategoryId = @rp{pIdx}
                        AND {string.Join(" AND ", conditions)}
                    )");
                    pIdx++;
                }
            }
            if (!string.IsNullOrEmpty(city))
            {
                whereClauses.Add("p.Address = @City");
                sqlParams.Add(new SqlParameter("@City", city));
            }
            var extraWhere = whereClauses.Count > 0
                ? "AND " + string.Join(" AND ", whereClauses)
                : "";

            var result = new List<Product>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
                SELECT p.Id, p.Name, p.Description, p.Address,
                       p.CreatedTime, p.AvatarImagePath,
                       u.UserName, u.AvatarImagePath, u.IsCompany,
                       u.PhoneNumber, u.Email, u.CreatedAt, u.Id
                       {priceSelect}
                FROM Products p
                LEFT JOIN Users u ON u.Id = p.UserId
                {priceJoin}
                WHERE p.CategoryId IN ({ids})
                {extraWhere}
                ORDER BY {order}", conn);

            cmd.Parameters.AddRange(sqlParams.ToArray());

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Description = r.IsDBNull(2) ? null : r.GetString(2),
                    Address = r.IsDBNull(3) ? null : r.GetString(3),
                    CreatedTime = r.IsDBNull(4) ? null : r.GetDateTime(4),
                    AvatarImagePath = r.IsDBNull(5) ? null : r.GetString(5),
                    Seller = new SellerViewModel
                    {
                        Id = r.IsDBNull(12) ? 0 : r.GetInt32(12),
                        UserName = r.IsDBNull(6) ? null : r.GetString(6),
                        AvatarPath = r.IsDBNull(7) ? null : r.GetString(7),
                        IsCompany = !r.IsDBNull(8) && r.GetBoolean(8),
                        PhoneNumber = r.IsDBNull(9) ? null : r.GetString(9),
                        Email = r.IsDBNull(10) ? null : r.GetString(10),
                        CreatedAt = r.IsDBNull(11) ? null : r.GetDateTime(11),
                    },
                    Price = r.IsDBNull(13) ? null : r.GetDecimal(13),
                });
            return result;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
    SELECT p.Id, p.Name, p.Description, p.Address,
           p.CreatedTime, p.AvatarImagePath, p.CategoryId,
           u.UserName, u.AvatarImagePath, u.IsCompany, u.PhoneNumber, u.Id,
           p.UserId, u.Email, u.CreatedAt
    FROM Products p
    LEFT JOIN Users u ON u.Id = p.UserId
    WHERE p.Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            var product = new Product
            {
                Id = r.GetInt32(0),
                Name = r.IsDBNull(1) ? "" : r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                Address = r.IsDBNull(3) ? null : r.GetString(3),
                CreatedTime = r.IsDBNull(4) ? null : r.GetDateTime(4),
                AvatarImagePath = r.IsDBNull(5) ? null : r.GetString(5),
                CategoryId = r.IsDBNull(6) ? null : r.GetInt32(6),
                UserId = r.IsDBNull(12) ? null : r.GetInt32(12),
                Seller = new SellerViewModel
                {
                    Id = r.IsDBNull(11) ? 0 : r.GetInt32(11),
                    UserName = r.IsDBNull(7) ? null : r.GetString(7),
                    AvatarPath = r.IsDBNull(8) ? null : r.GetString(8),
                    IsCompany = !r.IsDBNull(9) && r.GetBoolean(9),
                    PhoneNumber = r.IsDBNull(10) ? null : r.GetString(10),
                    Email = r.IsDBNull(13) ? null : r.GetString(13),
                    CreatedAt = r.IsDBNull(14) ? null : r.GetDateTime(14),
                },
            };
            await r.CloseAsync();

            // Загружаем цену из MapperProductCategory
            await using var priceCmd = new SqlCommand(@"
                SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                FROM MapperProductCategory m
                JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                WHERE m.ProductId = @Id", conn);
            priceCmd.Parameters.AddWithValue("@Id", id);
            var priceVal = await priceCmd.ExecuteScalarAsync();
            product.Price = priceVal == null || priceVal == DBNull.Value
                ? null
                : Convert.ToDecimal(priceVal);

            return product;
        }

        public async Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8)
        {
            var result = new List<Product>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
                SELECT TOP {count}
                       p.Id, p.Name, p.Address, p.CreatedTime, p.AvatarImagePath,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                        WHERE m.ProductId = p.Id) as Price
                FROM Products p
                WHERE p.CategoryId = @CatId AND p.Id != @ExcId
                ORDER BY p.CreatedTime DESC", conn);
            cmd.Parameters.AddWithValue("@CatId", categoryId);
            cmd.Parameters.AddWithValue("@ExcId", excludeId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedTime = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarImagePath = r.IsDBNull(4) ? null : r.GetString(4),
                    Price = r.IsDBNull(5) ? null : r.GetDecimal(5),
                });
            return result;
        }

        public async Task<List<Product>> GetByUserAsync(int userId, string status = "active")
        {
            var result = new List<Product>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT p.Id, p.Name, p.Address, p.CreatedTime, p.AvatarImagePath, p.Status,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                        WHERE m.ProductId = p.Id) as Price
                FROM Products p
                WHERE p.UserId = @UserId
                  AND (p.Status = @Status OR (@Status = 'active' AND p.Status IS NULL))
                ORDER BY p.CreatedTime DESC", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Status", status);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedTime = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarImagePath = r.IsDBNull(4) ? null : r.GetString(4),
                    Price = r.IsDBNull(6) ? null : r.GetDecimal(6),
                });
            return result;
        }

        public async Task UpdateAsync(Product product)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                UPDATE Products SET
                    Name        = @Name,
                    Description = @Description,
                    Qty         = @Qty,
                    Address     = @Address,
                    CategoryId  = @CategoryId
                WHERE Id = @Id AND UserId = @UserId", conn);
            cmd.Parameters.AddWithValue("@Name", product.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Qty", (object?)product.Qty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object?)product.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", product.Id);
            cmd.Parameters.AddWithValue("@UserId", product.UserId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Dictionary<int, string>> GetParamValuesAsync(int productId)
        {
            var result = new Dictionary<int, string>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT CategoryId, Value FROM MapperProductCategory WHERE ProductId = @ProductId", conn);
            cmd.Parameters.AddWithValue("@ProductId", productId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result[r.GetInt32(0)] = r.IsDBNull(1) ? "" : r.GetString(1);
            return result;
        }
    }
}