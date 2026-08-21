using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ProductRepository : SqlRepositoryBase, IProductRepository
    {
        public ProductRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<CategoryRulesDto> GetCategoryRulesAsync(IEnumerable<int> categoryIds)
        {
            var ids = string.Join(",", categoryIds);
            var result = new CategoryRulesDto();

            await using var conn = await OpenConnectionAsync();

            await using var cmd = new SqlCommand($@"
        SELECT TargetParamId, TriggerParamId, TriggerValue, TriggerOperator, Action
        FROM ParameterVisibilityRules WHERE CategoryId IN ({ids});

        SELECT ParamId, RuleType, RuleValue, TriggerParamId, TriggerValue, ErrorMessageKey
        FROM ParameterValidationRules
        WHERE ParamId IN (
            SELECT Id FROM Category WHERE ParentId IN ({ids}) AND Type IN (2,3,4,5,6,7,8)
        );

        SELECT ScriptPath FROM ParameterCustomScripts WHERE CategoryId IN ({ids});
    ", conn);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                result.VisibilityRules.Add(new VisibilityRuleDto
                {
                    TargetParamId = reader.GetInt32(0),
                    TriggerParamId = reader.GetInt32(1),
                    TriggerValue = reader.IsDBNull(2) ? null : reader.GetString(2),
                    TriggerOperator = reader.GetString(3),
                    Action = reader.GetString(4)
                });

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                result.ValidationRules.Add(new ValidationRuleDto
                {
                    ParamId = reader.GetInt32(0),
                    RuleType = reader.GetString(1),
                    RuleValue = reader.IsDBNull(2) ? null : reader.GetString(2),
                    TriggerParamId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    TriggerValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ErrorMessageKey = reader.IsDBNull(5) ? null : reader.GetString(5)
                });

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                result.CustomScriptPaths.Add(reader.GetString(0));

            return result;
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
            if (string.IsNullOrEmpty(ids)) return [];

            var priceJoin = priceParamId.HasValue
                ? $"LEFT JOIN MapperProductCategory mpc ON mpc.ProductId = p.Id AND mpc.CategoryId = {priceParamId}"
                : "";
            var priceSelect = priceParamId.HasValue
                ? ", TRY_CAST(mpc.Value AS decimal(18,2)) as Price"
                : @", (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
           FROM MapperProductCategory m
           JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
           WHERE m.ProductId = p.Id) as Price";
            var baseOrder = sort switch
            {
                "cheap" => priceParamId.HasValue ? "TRY_CAST(mpc.Value AS decimal(18,2)) ASC" : "p.CreatedAt DESC",
                "expensive" => priceParamId.HasValue ? "TRY_CAST(mpc.Value AS decimal(18,2)) DESC" : "p.CreatedAt DESC",
                _ => "p.CreatedAt DESC"
            };
            var order = @"CASE 
                WHEN p.PromotionType IN ('Top','Vip') THEN 0 
                WHEN p.PromotionType = 'Highlight' THEN 1 
                ELSE 2 
              END, " + baseOrder;
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
                ? $"AND (({string.Join(" AND ", whereClauses)}) OR p.PromotionType IN ('Top','Vip'))"
                : "";

            return await QueryAsync($@"
                SELECT p.Id, p.Name, p.Description, p.Address,
                       p.CreatedAt, COALESCE(
                   (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
                    WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                   p.AvatarUrl
               ) AS AvatarUrl,
                       u.UserName, u.AvatarUrl, u.IsCompany,
                       u.Phone, u.Email, u.CreatedAt, u.Id, p.PromotionType
                       {priceSelect}
                FROM Products p
                LEFT JOIN Users u ON u.Id = p.UserId
                {priceJoin}
                WHERE p.CategoryId IN ({ids})
                AND (p.Status = 'active' OR p.Status IS NULL)
                {extraWhere}
                ORDER BY {order}",
                r => new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Description = r.IsDBNull(2) ? null : r.GetString(2),
                    Address = r.IsDBNull(3) ? null : r.GetString(3),
                    CreatedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
                    AvatarUrl = r.IsDBNull(5) ? null : r.GetString(5),
                    Seller = new UserSummary
                    {
                        Id = r.IsDBNull(12) ? 0 : r.GetInt32(12),
                        UserName = r.IsDBNull(6) ? null : r.GetString(6),
                        AvatarUrl = r.IsDBNull(7) ? null : r.GetString(7),
                        IsCompany = !r.IsDBNull(8) && r.GetBoolean(8),
                        Phone = r.IsDBNull(9) ? null : r.GetString(9),
                        Email = r.IsDBNull(10) ? null : r.GetString(10),
                        CreatedAt = r.IsDBNull(11) ? null : r.GetDateTime(11),
                    },
                    Price = r.IsDBNull(14) ? null : r.GetDecimal(14),
                    PromotionType = r.IsDBNull(13) ? "None" : r.GetString(13),
                },
                p =>
                {
                    foreach (var sqlParam in sqlParams) p.Add(sqlParam);
                });
        }
        public async Task<Dictionary<int, string>> GetParamDisplayValuesAsync(int productId, string lang)
        {
            var options = await LoadSelectOptionsDictionaryAsync();
            var colors = await LoadColorOptionsDictionaryAsync();

            var rawValues = await QueryAsync(
                @"SELECT m.CategoryId, m.Value, c.Type
                  FROM MapperProductCategory m
                  JOIN Category c ON c.Id = m.CategoryId
                  WHERE m.ProductId = @ProductId",
                r => (
                    CategoryId: r.GetInt32(0),
                    Value: r.IsDBNull(1) ? "" : r.GetString(1),
                    Type: r.IsDBNull(2) ? (int?)null : r.GetInt32(2)
                ),
                p => p.AddWithValue("@ProductId", productId));

            var result = new Dictionary<int, string>();
            var multiValues = new Dictionary<int, List<string>>();

            foreach (var (paramId, rawValue, type) in rawValues)
            {
                string text;
                if (type == 2 || type == 8)
                    text = ResolveOptionTextFromDictionary(rawValue, options, lang);
                else if (type == 4)
                {
                    var ids = rawValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var texts = ids.Select(id => ResolveOptionTextFromDictionary(id, options, lang));
                    if (!multiValues.ContainsKey(paramId))
                        multiValues[paramId] = [];
                    multiValues[paramId].AddRange(texts);
                    continue;
                }
                else if (type == 6)
                    if (int.TryParse(rawValue, out int colorId) && colors.TryGetValue(colorId, out var c))
                        text = Resolve(lang, c.Name, c.NameLV, c.NameRU);
                    else text = rawValue;
                else
                    text = rawValue;

                result[paramId] = text;
            }

            foreach (var (paramId, list) in multiValues)
                result[paramId] = string.Join(", ", list);

            return result;
        }
        private string ResolveOptionTextFromDictionary(string idStr, Dictionary<int, (string Value, string ValueLV, string ValueRU)> options, string lang)
        {
            if (!int.TryParse(idStr, out int id) || !options.TryGetValue(id, out var texts))
                return idStr;
            return Resolve(lang, texts.Value, texts.ValueLV, texts.ValueRU);
        }
        private async Task<Dictionary<int, (string Value, string ValueLV, string ValueRU)>> LoadSelectOptionsDictionaryAsync()
        {
            var data = await QueryAsync(
                "SELECT Id, Value, ValueLV, ValueRU FROM SelectOptions where IsConf = 1",
                r => (
                    Id: r.GetInt32(0),
                    Value: r.GetString(1),
                    ValueLV: r.IsDBNull(2) ? r.GetString(1) : r.GetString(2),
                    ValueRU: r.IsDBNull(3) ? r.GetString(1) : r.GetString(3)
                ));
            return data.ToDictionary(x => x.Id, x => (x.Value, x.ValueLV, x.ValueRU));
        }
        private async Task<Dictionary<int, (string Name, string NameLV, string NameRU, string Hex)>> LoadColorOptionsDictionaryAsync()
        {
            var data = await QueryAsync(
                "SELECT Id, Name, NameLV, NameRU, HexValue FROM ColorOptions WHERE IsConf = 1",
                r => (
                    Id: r.GetInt32(0),
                    Name: r.GetString(1),
                    NameLV: r.IsDBNull(2) ? r.GetString(1) : r.GetString(2),
                    NameRU: r.IsDBNull(3) ? r.GetString(1) : r.GetString(3),
                    Hex: r.GetString(4)
                ));
            return data.ToDictionary(x => x.Id, x => (x.Name, x.NameLV, x.NameRU, x.Hex));
        }
        public async Task<Product?> GetByIdAsync(int id)
        {
            var product = await QuerySingleAsync(
                @"
    SELECT p.Id, p.Name, p.Description, p.Address,
           p.CreatedAt, COALESCE(
       (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
        WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
       p.AvatarUrl
   ) AS AvatarUrl, p.CategoryId,
           p.Status, p.Qty,
           u.UserName, u.AvatarUrl, u.IsCompany, u.Phone, u.Id,
           p.UserId, u.Email, u.CreatedAt, p.PromotionType
    FROM Products p
    LEFT JOIN Users u ON u.Id = p.UserId
    WHERE p.Id = @Id",
                r => new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Description = r.IsDBNull(2) ? null : r.GetString(2),
                    Address = r.IsDBNull(3) ? null : r.GetString(3),
                    CreatedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
                    AvatarUrl = r.IsDBNull(5) ? null : r.GetString(5),
                    CategoryId = r.IsDBNull(6) ? null : r.GetInt32(6),
                    Status = r.IsDBNull(7) ? ProductStatus.Active : Enum.Parse<ProductStatus>(r.GetString(7), true),
                    Qty = r.IsDBNull(8) ? null : r.GetInt32(8),
                    UserId = r.IsDBNull(14) ? null : r.GetInt32(14),
                    Seller = new UserSummary
                    {
                        Id = r.IsDBNull(13) ? 0 : r.GetInt32(13),
                        UserName = r.IsDBNull(9) ? null : r.GetString(9),
                        AvatarUrl = r.IsDBNull(10) ? null : r.GetString(10),
                        IsCompany = !r.IsDBNull(11) && r.GetBoolean(11),
                        Phone = r.IsDBNull(12) ? null : r.GetString(12),
                        Email = r.IsDBNull(15) ? null : r.GetString(15),
                        CreatedAt = r.IsDBNull(16) ? null : r.GetDateTime(16),
                    },
                    PromotionType = r.IsDBNull(17) ? "None" : r.GetString(17),
                },
                p => p.AddWithValue("@Id", id));

            if (product == null) return null;

            var priceVal = await QueryAsync<decimal?>(
                @"SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                  FROM MapperProductCategory m
                  JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                  WHERE m.ProductId = @Id",
                r => r.IsDBNull(0) ? (decimal?)null : r.GetDecimal(0),
                p => p.AddWithValue("@Id", id));

            product.Price = priceVal.FirstOrDefault();
            product.Media = await GetMediaAsync(id);

            return product;
        }
        public async Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8)
        {
            return await QueryAsync($@"
                SELECT TOP {count}
                       p.Id, p.Name, p.Address, p.CreatedAt, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarUrl
       ) AS AvatarUrl,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                        WHERE m.ProductId = p.Id) as Price
                FROM Products p
                WHERE p.CategoryId = @CatId AND p.Id != @ExcId
                ORDER BY p.CreatedAt DESC",
                r => new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarUrl = r.IsDBNull(4) ? null : r.GetString(4),
                    Price = r.IsDBNull(5) ? null : r.GetDecimal(5),
                },
                p =>
                {
                    p.AddWithValue("@CatId", categoryId);
                    p.AddWithValue("@ExcId", excludeId);
                });
        }
        public async Task<List<Product>> GetByUserAsync(int userId, string status = "active")
        {
            return await QueryAsync(@"
        SELECT p.Id, p.Name, p.Address, p.CreatedAt, COALESCE(
           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
           p.AvatarUrl
       ) AS AvatarUrl, p.Status,
               (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                FROM MapperProductCategory m
                JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                WHERE m.ProductId = p.Id) as Price,
               ISNULL(p.ViewCount, 0),
               (SELECT COUNT(*) FROM Favourites WHERE ProductId = p.Id AND Can = 1) as FavCount,
               (SELECT COUNT(*) FROM Favourites WHERE ProductId = p.Id AND Can = 0) as CartCount
        FROM Products p
        WHERE p.UserId = @UserId
          AND (p.Status = @Status)
        ORDER BY p.CreatedAt DESC",
                r => new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarUrl = r.IsDBNull(4) ? null : r.GetString(4),
                    Status = r.IsDBNull(5) ? ProductStatus.Active : Enum.Parse<ProductStatus>(r.GetString(5), true),
                    Price = r.IsDBNull(6) ? null : r.GetDecimal(6),
                    ViewCount = r.GetInt32(7),
                    FavCount = r.GetInt32(8),
                    CartCount = r.GetInt32(9),
                },
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Status", status);
                });
        }
        public async Task UpdateAsync(Product product, Dictionary<int, string> paramValues, string promotionType = "None")
        {
            var allowedPromotions = new[] { "None", "Highlight", "Top", "Vip" };
            if (!allowedPromotions.Contains(promotionType))
                promotionType = "None";

            await using var conn = await OpenConnectionAsync();

            await using var cmd = new SqlCommand(@"
            UPDATE Products SET
                Name        = @Name,
                Description = @Description,
                Qty         = @Qty,
                Address     = @Address,
                CategoryId  = @CategoryId,
                PromotionType = @PromotionType
            WHERE Id = @Id AND UserId = @UserId", conn);

            cmd.Parameters.AddWithValue("@Name", product.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Qty", (object?)product.Qty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object?)product.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PromotionType", promotionType);
            cmd.Parameters.AddWithValue("@Id", product.Id);
            cmd.Parameters.AddWithValue("@UserId", product.UserId!);
            await cmd.ExecuteNonQueryAsync();

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
        }
        public async Task<Dictionary<string, int>> GetCountsByStatusAsync(int userId)
        {
            var data = await QueryAsync(
                "SELECT Status, COUNT(*) FROM Products WHERE UserId = @UserId GROUP BY Status",
                r => (Status: r.GetString(0), Count: r.GetInt32(1)),
                p => p.AddWithValue("@UserId", userId));

            return data.ToDictionary(x => x.Status, x => x.Count);
        }
        public async Task<Dictionary<int, string>> GetParamValuesAsync(int productId)
        {
            var data = await QueryAsync(
                "SELECT CategoryId, Value FROM MapperProductCategory WHERE ProductId = @ProductId",
                r => (CategoryId: r.GetInt32(0), Value: r.IsDBNull(1) ? "" : r.GetString(1)),
                p => p.AddWithValue("@ProductId", productId));

            return data.ToDictionary(x => x.CategoryId, x => x.Value);
        }
        public async Task<bool> CompleteDealAsync(int productId, int sellerId, int buyerId)
        {
            await using var conn = await OpenConnectionAsync();
            await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

            try
            {
                var selectSql = @"
            SELECT 
                p.AvatarUrl,
                p.Qty,
                (SELECT mpc.Value 
                 FROM MapperProductCategory mpc
                 INNER JOIN Category c ON mpc.CategoryId = c.Id
                 WHERE mpc.ProductId = p.Id AND c.Name = 'Price, €') AS Price,
                (SELECT mpc.Value 
                 FROM MapperProductCategory mpc
                 INNER JOIN Category c ON mpc.CategoryId = c.Id
                 WHERE mpc.ProductId = p.Id AND c.Name = 'With delivery') AS Delivery
            FROM Products p
            WHERE p.Id = @ProductId AND p.UserId = @SellerId AND p.Status = 'Active'";

                await using var selectCmd = new SqlCommand(selectSql, conn, transaction);
                selectCmd.Parameters.AddWithValue("@ProductId", productId);
                selectCmd.Parameters.AddWithValue("@SellerId", sellerId);

                string imagePath = null;
                int qty = 0;
                decimal? price = 0;
                string delivery = "";

                await using (var reader = await selectCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    imagePath = reader.IsDBNull(0) ? null : reader.GetString(0);
                    qty = reader.GetInt32(1);
                    price = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]);
                    delivery = reader.IsDBNull(3) ? "" : reader.GetString(3);
                }

                if (!string.IsNullOrEmpty(imagePath))
                {
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/'));
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }

                var updateSql = @"
            UPDATE Products 
            SET Status = 'Succeeded', 
                AvatarUrl = NULL,
                ArchivedAt = GETDATE()
            WHERE Id = @ProductId AND UserId = @SellerId AND Status = 'Active'";

                await using var updateCmd = new SqlCommand(updateSql, conn, transaction);
                updateCmd.Parameters.AddWithValue("@ProductId", productId);
                updateCmd.Parameters.AddWithValue("@SellerId", sellerId);

                var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                var insertSql = @"
            INSERT INTO Orders (OrderStatus, CreatedAt, Delivery, Qty, Price, ProductId, UserId)
            VALUES (@OrderStatus, GETDATE(), @Delivery, @Qty, @Price, @ProductId, @BuyerId)";

                await using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                insertCmd.Parameters.AddWithValue("@OrderStatus", 1);
                insertCmd.Parameters.AddWithValue("@Delivery", delivery);
                insertCmd.Parameters.AddWithValue("@Qty", qty);
                insertCmd.Parameters.AddWithValue("@Price", price.Value);
                insertCmd.Parameters.AddWithValue("@ProductId", productId);
                insertCmd.Parameters.AddWithValue("@BuyerId", buyerId);

                await insertCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> ReactivateProductAsync(int productId, int userId)
        {
            var rowsAffected = await ExecuteAsync(@"
        UPDATE Products 
        SET Status = 'Active', 
            ArchivedAt = NULL,
            CreatedAt = SYSUTCDATETIME(),
            ExpiresAt = DATEADD(day, PublishDurationDays, SYSUTCDATETIME())
        WHERE Id = @Id AND UserId = @UserId AND Status = 'Archived'",
                p =>
                {
                    p.AddWithValue("@Id", productId);
                    p.AddWithValue("@UserId", userId);
                });
            return rowsAffected > 0;
        }
        public async Task ArchiveProductsByUserAsync(int userId)
        {
            await ExecuteAsync(@"
        UPDATE Products 
        SET Status = 'Archived', 
            ArchivedAt = GETUTCDATE() 
        WHERE UserId = @UserId AND Status != 'Archived'",
                p => p.AddWithValue("@UserId", userId));
        }
        public async Task<IEnumerable<Product>> GetUserProductsByStatusAsync(int userId, string status)
        {
            return await QueryAsync(
                "SELECT * FROM Products WHERE UserId = @UserId AND Status = @Status ORDER BY CreatedAt DESC",
                r => MapProduct(r),
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Status", status);
                });
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
                AvatarUrl = reader.IsDBNull(reader.GetOrdinal("AvatarUrl")) ? null : reader.GetString(reader.GetOrdinal("AvatarUrl")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

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
            await using var conn = await OpenConnectionAsync();
            await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();

            try
            {
                var updateSql = "UPDATE Products SET Status = @Status WHERE Id = @Id";
                int rowsAffected;
                using (var updateCommand = new SqlCommand(updateSql, conn, transaction))
                {
                    updateCommand.Parameters.AddWithValue("@Status", status.ToString());
                    updateCommand.Parameters.AddWithValue("@Id", productId);

                    rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                }

                var deleteSql = "DELETE FROM Favourites WHERE ProductId = @Id AND Can = 1";
                using (var deleteCommand = new SqlCommand(deleteSql, conn, transaction))
                {
                    deleteCommand.Parameters.AddWithValue("@Id", productId);
                    await deleteCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return rowsAffected > 0;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<int> CreateAsync(Product product, Dictionary<int, string> paramValues, int publishDurationDays = 30, string promotionType = "None")
        {
            if (!new[] { 7, 14, 30, 60, 90 }.Contains(publishDurationDays))
                publishDurationDays = 30;

            var allowedPromotions = new[] { "None", "Highlight", "Top", "Vip" };
            if (!allowedPromotions.Contains(promotionType))
                promotionType = "None";

            await using var conn = await OpenConnectionAsync();

            await using var cmd = new SqlCommand(@"
        INSERT INTO Products (Name, Description, Qty, Address, CategoryId, UserId, AvatarUrl,
                              CreatedAt, Status, PublishDurationDays, ExpiresAt, PromotionType)
        OUTPUT INSERTED.Id
        VALUES (@Name, @Description, @Qty, @Address, @CategoryId, @UserId, @AvatarUrl,
                GETDATE(), 'Active', @Duration, DATEADD(DAY, @Duration, GETDATE()), @PromotionType)", conn);

            cmd.Parameters.AddWithValue("@Name", product.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Qty", (object?)product.Qty ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Address", (object?)product.Address ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", product.UserId!);
            cmd.Parameters.AddWithValue("@AvatarUrl", (object?)product.AvatarUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Duration", publishDurationDays);
            cmd.Parameters.AddWithValue("@PromotionType", promotionType);

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
            return await QueryAsync(
                "SELECT Id, FilePath, MediaType, SortOrder FROM ProductMedia WHERE ProductId = @Id ORDER BY SortOrder",
                r => new ProductMedia
                {
                    Id = r.GetInt32(0),
                    ProductId = productId,
                    FilePath = r.GetString(1),
                    MediaType = r.GetString(2),
                    SortOrder = r.GetInt32(3),
                },
                p => p.AddWithValue("@Id", productId));
        }
        public async Task SaveMediaAsync(int productId, List<ProductMedia> media)
        {
            await using var conn = await OpenConnectionAsync();
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
            await using var conn = await OpenConnectionAsync();

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
            await ExecuteAsync(
                "UPDATE Products SET ViewCount = ViewCount + 1 WHERE Id = @Id",
                p => p.AddWithValue("@Id", productId));
        }
        public async Task DeleteAsync(int productId)
        {
            await using var conn = await OpenConnectionAsync();
            await DeleteProductCascade(conn, productId);
        }
        private async Task DeleteProductCascade(SqlConnection conn, int productId)
        {
            await using var mediaCmd = new SqlCommand(
                "SELECT FilePath FROM ProductMedia WHERE ProductId = @Id", conn);
            mediaCmd.Parameters.AddWithValue("@Id", productId);
            await using var mediaReader = await mediaCmd.ExecuteReaderAsync();
            var paths = new List<string>();
            while (await mediaReader.ReadAsync())
                paths.Add(mediaReader.GetString(0));
            await mediaReader.CloseAsync();

            foreach (var path in paths)
            {
                var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/'));
                if (File.Exists(full)) File.Delete(full);
            }

            var steps = new[]
            {
        "DELETE FROM ProductMedia          WHERE ProductId = @Id",
        "DELETE FROM MapperProductCategory WHERE ProductId = @Id",
        "DELETE FROM Favourites            WHERE ProductId = @Id",
        "DELETE FROM Reports               WHERE ProductId = @Id",
        "DELETE FROM Notifications         WHERE ProductId = @Id",
        "DELETE FROM Messages              WHERE ConversationId IN (SELECT Id FROM Conversations WHERE ProductId = @Id)",
        "DELETE FROM Conversations         WHERE ProductId = @Id",
        "DELETE FROM Reviews               WHERE ProductId = @Id",
        "DELETE FROM Products              WHERE Id = @Id",
    };

            foreach (var sql in steps)
            {
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", productId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        public async Task<(List<AdminConfOptionRow> Items, int TotalCount)> GetUnconfirmedOptionsAsync()
        {
            var items = new List<AdminConfOptionRow>();
            int totalCount = 0;

            await using var conn = await OpenConnectionAsync();

            var sql = @"
                        SELECT COUNT(*) FROM dbo.SelectOptions so
                        INNER JOIN dbo.MapperProductCategory mpc 
                            ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%'
                        INNER JOIN dbo.Products p ON mpc.ProductId = p.Id
                        WHERE so.IsConf = 0;

                        SELECT 
                            so.Id AS OptionId,
                            so.Value AS OptionValue,
                            so.ValueLV AS OptionValueLV,
                            so.ValueRU AS OptionValueRU,
                            p.Id AS ProductId,
                            p.Name AS ProductName,
                            p.CreatedAt AS CreatedAt,
                            u.Id AS UserId,
                            u.UserName AS UserName,
                            c.Id AS CategoryId,
                            c.Name AS CategoryName,
                            c2.Name AS OptionCategory,
                            c.NameLV AS CategoryNameLV,
                            c2.NameLV AS OptionCategoryLV,
                            c.NameRU AS CategoryNameRU,
                            c2.NameRU AS OptionCategoryRU
                        FROM dbo.SelectOptions so
                        INNER JOIN dbo.MapperProductCategory mpc 
                            ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%'
                        INNER JOIN dbo.Products p ON mpc.ProductId = p.Id
                        INNER JOIN dbo.Users u ON p.UserId = u.Id
                        INNER JOIN dbo.Category c ON p.CategoryId = c.Id
                        INNER JOIN dbo.Category c2 ON so.CategoryId = c2.Id
                        WHERE so.IsConf = 0;";

            await using var cmd = new SqlCommand(sql, conn);

            await using var r = await cmd.ExecuteReaderAsync();

            if (await r.ReadAsync())
            {
                totalCount = r.GetInt32(0);
            }

            await r.NextResultAsync();
            while (await r.ReadAsync())
            {
                items.Add(new AdminConfOptionRow
                {
                    OptionId = r.GetInt32(0),
                    OptionValue = r.IsDBNull(1) ? "" : r.GetString(1),
                    OptionValueLV = r.IsDBNull(2) ? "" : r.GetString(2),
                    OptionValueRU = r.IsDBNull(3) ? "" : r.GetString(3),
                    ProductId = r.GetInt32(4),
                    ProductName = r.IsDBNull(5) ? "" : r.GetString(5),
                    CreatedAt = r.IsDBNull(6) ? null : r.GetDateTime(6),
                    UserId = r.GetInt32(7),
                    UserName = r.IsDBNull(8) ? "" : r.GetString(8),
                    CategoryId = r.GetInt32(9),
                    CategoryName = r.IsDBNull(10) ? "" : r.GetString(10),
                    ParameterName = r.IsDBNull(11) ? "" : r.GetString(11),
                    CategoryNameLV = r.IsDBNull(12) ? "" : r.GetString(12),
                    ParameterNameLV = r.IsDBNull(13) ? "" : r.GetString(13),
                    CategoryNameRU = r.IsDBNull(14) ? "" : r.GetString(14),
                    ParameterNameRU = r.IsDBNull(15) ? "" : r.GetString(15)
                });
            }

            return (items, totalCount);
        }
        public async Task<bool> ApproveSelectOptionAsync(int optionId)
        {
            var rowsAffected = await ExecuteAsync(
                "UPDATE SelectOptions SET IsConf = 1 WHERE Id = @Id",
                p => p.AddWithValue("@Id", optionId));
            return rowsAffected > 0;
        }
        public async Task<bool> RejectProductAndOptionAsync(int optionId, int productId)
        {
            await using var conn = await OpenConnectionAsync();
            await using var transaction = conn.BeginTransaction();

            try
            {
                var productSql = "UPDATE Products SET Status = 'Rejected' WHERE Id = @ProductId";
                await using var productCmd = new SqlCommand(productSql, conn, transaction);
                productCmd.Parameters.AddWithValue("@ProductId", productId);
                await productCmd.ExecuteNonQueryAsync();

                var optionSql = "DELETE FROM SelectOptions WHERE Id = @OptionId";
                await using var optionCmd = new SqlCommand(optionSql, conn, transaction);
                optionCmd.Parameters.AddWithValue("@OptionId", optionId);
                await optionCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
        public async Task<int?> GetPriceParamIdAsync(int productId)
        {
            var result = await QueryAsync<int?>(
                @"SELECT TOP 1 mpc.CategoryId
                  FROM MapperProductCategory mpc
                  JOIN Category c ON c.Id = mpc.CategoryId AND c.Name = 'Price, €'
                  WHERE mpc.ProductId = @Id",
                r => r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                p => p.AddWithValue("@Id", productId));
            return result.FirstOrDefault();
        }
        public async Task<int> RecalculateModerationScoreAsync(int productId)
        {
            await using var conn = await OpenConnectionAsync();

            await using var confCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM MapperProductCategory mpc
                JOIN SelectOptions so ON TRY_CAST(mpc.Value AS int) = so.Id
                JOIN Category c ON c.Id = mpc.CategoryId AND c.Type IN (2, 4, 8)
                WHERE mpc.ProductId = @Id AND so.IsConf = 0", conn);
            confCmd.Parameters.AddWithValue("@Id", productId);
            var unconfCount = (int)(await confCmd.ExecuteScalarAsync())!;

            await using var scoreCmd = new SqlCommand(@"
                UPDATE Products SET ModerationScore = @Score,
                    Status = CASE WHEN @Score >= 5 THEN 'Moderation' ELSE Status END
                WHERE Id = @Id", conn);
            scoreCmd.Parameters.AddWithValue("@Score", unconfCount);
            scoreCmd.Parameters.AddWithValue("@Id", productId);
            await scoreCmd.ExecuteNonQueryAsync();

            return unconfCount;
        }
        public async Task<List<int>> GetFavouriteSubscriberIdsAsync(int productId, int excludeUserId)
        {
            return await QueryAsync(
                "SELECT DISTINCT UserId FROM Favourites WHERE ProductId = @ProductId AND Can = 1 AND UserId != @ExcludeUserId",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@ProductId", productId);
                    p.AddWithValue("@ExcludeUserId", excludeUserId);
                });
        }
        public async Task<List<Product>> GetPurchasedByUserAsync(int userId)
        {
            return await QueryAsync(
                @"SELECT p.Id, p.Name, p.Address, p.CreatedAt,
                COALESCE(
                    (SELECT TOP 1 pm.FilePath FROM ProductMedia pm 
                     WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                    p.AvatarUrl
                ) AS AvatarUrl,
                p.Status,
                o.Price AS Price
                 FROM Products p
                 INNER JOIN (
                     SELECT ProductId, Price, CreatedAt,
                            ROW_NUMBER() OVER (PARTITION BY ProductId ORDER BY CreatedAt DESC) AS rn
                     FROM Orders
                     WHERE UserId = @UserId AND OrderStatus = 1
                 ) o ON o.ProductId = p.Id AND o.rn = 1
                 ORDER BY o.CreatedAt DESC",
                r => new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Address = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarUrl = r.IsDBNull(4) ? null : r.GetString(4),
                    Status = ProductStatus.Succeeded,
                    Price = r.IsDBNull(6) ? null : r.GetDecimal(6),
                },
                p => p.AddWithValue("@UserId", userId));
        }
        public async Task<int> GetPurchasedConversationCountAsync(int userId)
        {
            var result = await QueryAsync<int>(
                @"SELECT COUNT(DISTINCT p.Id)
                FROM Products p
                JOIN Conversations conv ON conv.ProductId = p.Id
                    AND conv.IsSystem = 1
                    AND conv.BuyerId = @UserId
                WHERE p.Status = 'Succeeded'",
                r => r.GetInt32(0),
                p => p.AddWithValue("@UserId", userId));
            return result[0];
        }
        public async Task DeleteSpecificMediaAsync(IEnumerable<int> mediaIds)
        {
            var ids = mediaIds.ToList();
            if (!ids.Any()) return;

            await using var conn = await OpenConnectionAsync();

            await using var sel = new SqlCommand(
                $"SELECT FilePath FROM ProductMedia WHERE Id IN ({string.Join(",", ids)})", conn);
            var paths = new List<string>();
            await using (var r = await sel.ExecuteReaderAsync())
                while (await r.ReadAsync()) paths.Add(r.GetString(0));

            foreach (var path in paths)
            {
                var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/'));
                if (File.Exists(full)) File.Delete(full);
            }

            await using var del = new SqlCommand(
                $"DELETE FROM ProductMedia WHERE Id IN ({string.Join(",", ids)})", conn);
            await del.ExecuteNonQueryAsync();
        }
        public async Task UpdatePublishDurationAsync(int productId, int userId, int days)
        {
            await ExecuteAsync(@"
                UPDATE Products
                SET PublishDurationDays = @D,
                    ExpiresAt = DATEADD(DAY, @D, GETDATE())
                WHERE Id = @Id AND UserId = @UserId",
                p =>
                {
                    p.AddWithValue("@D", days);
                    p.AddWithValue("@Id", productId);
                    p.AddWithValue("@UserId", userId);
                });
        }
        public async Task<List<int>> GetSubscriberIdsExcludingAsync(int sellerId, int excludeBuyerId)
        {
            return await QueryAsync(
                "SELECT FollowerId FROM Subscriptions WHERE FollowingId = @SellerId AND FollowerId != @BuyerId",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@SellerId", sellerId);
                    p.AddWithValue("@BuyerId", excludeBuyerId);
                });
        }
    }
}