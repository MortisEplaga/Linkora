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
            string? city = null,
            string? search = null)
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
            if (!string.IsNullOrEmpty(search))
            {
                whereClauses.Add("p.Name LIKE '%' + @SearchTerm + '%'");
                sqlParams.Add(new SqlParameter("@SearchTerm", search));
            }
            var extraWhere = whereClauses.Count > 0
                ? "AND " + string.Join(" AND ", whereClauses)
                : "";

            var result = new List<Product>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
                SELECT p.Id, p.Name, p.Description, p.Address,
                       p.CreatedTime, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarImagePath
       ) AS AvatarImagePath,
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
               p.CreatedTime, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarImagePath
       ) AS AvatarImagePath, p.CategoryId,
               p.Status,
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
                // Статус
                Status = r.IsDBNull(7)
           ? ProductStatus.Active
           : Enum.Parse<ProductStatus>(r.GetString(7), true),
                UserId = r.IsDBNull(13) ? null : r.GetInt32(13),
                Seller = new SellerViewModel
                {
                    Id = r.IsDBNull(12) ? 0 : r.GetInt32(12),
                    UserName = r.IsDBNull(8) ? null : r.GetString(8),
                    AvatarPath = r.IsDBNull(9) ? null : r.GetString(9),
                    IsCompany = !r.IsDBNull(10) && r.GetBoolean(10),
                    PhoneNumber = r.IsDBNull(11) ? null : r.GetString(11),
                    Email = r.IsDBNull(14) ? null : r.GetString(14),
                    CreatedAt = r.IsDBNull(15) ? null : r.GetDateTime(15),
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

            product.Media = await GetMediaAsync(id);

            return product;
        }

        public async Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8)
        {
            var result = new List<Product>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand($@"
                SELECT TOP {count}
                       p.Id, p.Name, p.Address, p.CreatedTime, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarImagePath
       ) AS AvatarImagePath,
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
        SELECT p.Id, p.Name, p.Address, p.CreatedTime, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarImagePath
       ) AS AvatarImagePath, p.Status,
               (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                FROM MapperProductCategory m
                JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price'
                WHERE m.ProductId = p.Id) as Price,
               ISNULL(p.ViewCount, 0),
               (SELECT COUNT(*) FROM Favourites WHERE ProductId = p.Id AND Can = 1) as FavCount,
               (SELECT COUNT(*) FROM Favourites WHERE ProductId = p.Id AND Can = 0) as CartCount
        FROM Products p
        WHERE p.UserId = @UserId
          AND (p.Status = @Status)
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
                    Status = r.IsDBNull(5) ? ProductStatus.Active : Enum.Parse<ProductStatus>(r.GetString(5), true),
                    Price = r.IsDBNull(6) ? null : r.GetDecimal(6),
                    ViewCount = r.GetInt32(7),
                    FavCount = r.GetInt32(8),
                    CartCount = r.GetInt32(9),
                });
            return result;
        }
        public async Task UpdateAsync(Product product, Dictionary<int, string> paramValues)
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
            cmd.Parameters.AddWithValue("@UserId", product.UserId!);
            await cmd.ExecuteNonQueryAsync();

            // Удаляем старые параметры и вставляем новые
            await using var del = new SqlCommand(
                "DELETE FROM MapperProductCategory WHERE ProductId = @Id", conn);
            del.Parameters.AddWithValue("@Id", product.Id);
            await del.ExecuteNonQueryAsync();

            foreach (var (paramId, value) in paramValues)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                await using var ins = new SqlCommand(@"
            INSERT INTO MapperProductCategory (ProductId, CategoryId, Value)
            VALUES (@ProductId, @CategoryId, @Value)", conn);
                ins.Parameters.AddWithValue("@ProductId", product.Id);
                ins.Parameters.AddWithValue("@CategoryId", paramId);
                ins.Parameters.AddWithValue("@Value", value);
                await ins.ExecuteNonQueryAsync();
            }
        }        // ProductRepository.cs
        public async Task<Dictionary<string, int>> GetCountsByStatusAsync(int userId)
        {
            var result = new Dictionary<string, int>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = @"
        SELECT Status, COUNT(*) 
        FROM Products 
        WHERE UserId = @UserId 
        GROUP BY Status";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var status = reader.GetString(0);
                var count = reader.GetInt32(1);
                result[status] = count;
            }
            return result;
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
        public async Task<bool> CompleteDealAsync(int productId, int userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Получаем путь к изображению перед архивацией
            var getImageSql = "SELECT AvatarImagePath FROM Products WHERE Id = @Id AND UserId = @UserId";
            using var getCommand = new SqlCommand(getImageSql, connection);
            getCommand.Parameters.AddWithValue("@Id", productId);
            getCommand.Parameters.AddWithValue("@UserId", userId);

            var imagePath = await getCommand.ExecuteScalarAsync() as string;

            // Удаляем файл с диска
            if (!string.IsNullOrEmpty(imagePath))
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }

            // Обновляем статус и очищаем путь к изображению
            var sql = @"
        UPDATE Products 
        SET Status = 'Succeeded', 
            AvatarImagePath = NULL,
            ArchivedAt = GETDATE()
        WHERE Id = @Id AND UserId = @UserId AND Status = 'Active'";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", productId);
            command.Parameters.AddWithValue("@UserId", userId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        public async Task<bool> ReactivateProductAsync(int productId, int userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
        UPDATE Products 
        SET Status = 'Active', 
            ArchivedAt = NULL
        WHERE Id = @Id AND UserId = @UserId AND Status = 'Archived'";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", productId);
            command.Parameters.AddWithValue("@UserId", userId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<Product>> GetUserProductsByStatusAsync(int userId, string status)
        {
            var products = new List<Product>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT * FROM Products 
                WHERE UserId = @UserId 
                AND Status = @Status 
                ORDER BY CreatedAt DESC";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@Status", status);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add(MapProduct(reader));
            }
            return products;
        }
        private Product MapProduct(SqlDataReader reader)
        {
            return new Product
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                Address = reader.GetString(reader.GetOrdinal("Address")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                AvatarImagePath = reader.IsDBNull(reader.GetOrdinal("AvatarImagePath")) ? null : reader.GetString(reader.GetOrdinal("AvatarImagePath")),
                CreatedTime = reader.GetDateTime(reader.GetOrdinal("CreatedTime")),

                // Новые поля
                Status = reader.IsDBNull(reader.GetOrdinal("Status"))
                    ? ProductStatus.Active
                    : Enum.Parse<ProductStatus>(reader.GetString(reader.GetOrdinal("Status"))),

                ArchivedAt = reader.IsDBNull(reader.GetOrdinal("ArchivedAt"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("ArchivedAt"))
            };
        }
        public async Task<bool> UpdateProductStatusAsync(int productId, ProductStatus status)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE Products SET Status = @Status WHERE Id = @Id";
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Status", status.ToString());
            command.Parameters.AddWithValue("@Id", productId);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        public async Task<int> CreateAsync(Product product, Dictionary<int, string> paramValues)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
        INSERT INTO Products (Name, Description, Qty, Address, CategoryId, UserId, AvatarImagePath, CreatedTime, Status)
        OUTPUT INSERTED.Id
        VALUES (@Name, @Description, @Qty, @Address, @CategoryId, @UserId, @AvatarImagePath, GETDATE(), 'Moderation')", conn);

            cmd.Parameters.AddWithValue("@Name", product.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Qty", (object?)product.Qty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object?)product.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", product.UserId!);
            cmd.Parameters.AddWithValue("@AvatarImagePath", (object?)product.AvatarImagePath ?? DBNull.Value);

            var newId = (int)(await cmd.ExecuteScalarAsync())!;

            foreach (var (paramId, value) in paramValues)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                await using var p = new SqlCommand(@"
            INSERT INTO MapperProductCategory (ProductId, CategoryId, Value)
            VALUES (@ProductId, @CategoryId, @Value)", conn);
                p.Parameters.AddWithValue("@ProductId", newId);
                p.Parameters.AddWithValue("@CategoryId", paramId);
                p.Parameters.AddWithValue("@Value", value);
                await p.ExecuteNonQueryAsync();
            }

            return newId;
        }
        public async Task<List<ProductMedia>> GetMediaAsync(int productId)
        {
            var result = new List<ProductMedia>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, FilePath, MediaType, SortOrder FROM ProductMedia WHERE ProductId = @Id ORDER BY SortOrder", conn);
            cmd.Parameters.AddWithValue("@Id", productId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new ProductMedia
                {
                    Id = r.GetInt32(0),
                    ProductId = productId,
                    FilePath = r.GetString(1),
                    MediaType = r.GetString(2),
                    SortOrder = r.GetInt32(3),
                });
            return result;
        }

        public async Task SaveMediaAsync(int productId, List<ProductMedia> media)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            for (int i = 0; i < media.Count; i++)
            {
                await using var ins = new SqlCommand(@"
            INSERT INTO ProductMedia (ProductId, FilePath, MediaType, SortOrder)
            VALUES (@ProductId, @FilePath, @MediaType, @SortOrder)", conn);
                ins.Parameters.AddWithValue("@ProductId", productId);
                ins.Parameters.AddWithValue("@FilePath", media[i].FilePath);
                ins.Parameters.AddWithValue("@MediaType", media[i].MediaType);
                ins.Parameters.AddWithValue("@SortOrder", i);
                await ins.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteMediaAsync(int productId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Получаем пути для удаления файлов с диска
            await using var sel = new SqlCommand(
                "SELECT FilePath FROM ProductMedia WHERE ProductId = @Id", conn);
            sel.Parameters.AddWithValue("@Id", productId);
            await using var r = await sel.ExecuteReaderAsync();
            var paths = new List<string>();
            while (await r.ReadAsync()) paths.Add(r.GetString(0));
            await r.CloseAsync();

            foreach (var path in paths)
            {
                var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/'));
                if (File.Exists(full)) File.Delete(full);
            }

            await using var del = new SqlCommand(
                "DELETE FROM ProductMedia WHERE ProductId = @Id", conn);
            del.Parameters.AddWithValue("@Id", productId);
            await del.ExecuteNonQueryAsync();
        }
        public async Task IncrementViewCountAsync(int productId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Products SET ViewCount = ViewCount + 1 WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", productId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}