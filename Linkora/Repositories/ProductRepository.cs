using Linkora.Models;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace Linkora.Repositories
{
    public class ProductRepository : SqlRepositoryBase, IProductRepository
    {
        public ProductRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<CategoryRulesDto> GetCategoryRulesAsync(IEnumerable<int> categoryIds)
        {
            var idList = categoryIds.ToList();
            if (idList.Count == 0) return new CategoryRulesDto();
            var (inClause, inParams) = BuildInClause(idList, "@cid");
            var result = new CategoryRulesDto();
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand($@"SELECT TargetParamId,TriggerParamId,TriggerValue,TriggerOperator,Action FROM ParameterVisibilityRules WHERE CategoryId IN ({inClause});SELECT ParamId,RuleType,RuleValue,TriggerParamId,TriggerValue,ErrorMessageKey FROM ParameterValidationRules WHERE ParamId IN (SELECT Id FROM Category WHERE ParentId IN ({inClause}) AND Type IN (2,3,4,5,6,7,8));SELECT ScriptPath FROM ParameterCustomScripts WHERE CategoryId IN ({inClause});", conn);
            foreach (var prm in inParams) cmd.Parameters.Add(prm);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) result.VisibilityRules.Add(new VisibilityRuleDto { TargetParamId = reader.GetInt32(0), TriggerParamId = reader.GetInt32(1), TriggerValue = reader.GetStringOrNull(2), TriggerOperator = reader.GetString(3), Action = reader.GetString(4) });
            await reader.NextResultAsync();
            while (await reader.ReadAsync()) result.ValidationRules.Add(new ValidationRuleDto { ParamId = reader.GetInt32(0), RuleType = reader.GetString(1), RuleValue = reader.GetStringOrNull(2), TriggerParamId = reader.GetInt32OrNull(3), TriggerValue = reader.GetStringOrNull(4), ErrorMessageKey = reader.GetStringOrNull(5) });
            await reader.NextResultAsync();
            while (await reader.ReadAsync()) result.CustomScriptPaths.Add(reader.GetString(0));
            return result;
        }
        public async Task<List<Product>> GetByCategoryAsync(int rootCategoryId, bool includeDescendants = true,
                                                            string sort = "new", Dictionary<int, List<string>>? filters = null,
                                                            Dictionary<int, decimal>? rangeFrom = null, Dictionary<int, decimal>? rangeTo = null,
                                                            int? priceParamId = null, string? city = null, string? search = null)
        {
            var sqlParams = new List<SqlParameter> { new("@RootCategoryId", rootCategoryId) };

            var priceJoin = priceParamId.HasValue ? "LEFT JOIN MapperProductCategory mpc ON mpc.ProductId = p.Id AND mpc.CategoryId = @PriceParamId" : "";
            if (priceParamId.HasValue) sqlParams.Add(new SqlParameter("@PriceParamId", priceParamId.Value));

            var priceSelect = priceParamId.HasValue
                ? ", TRY_CAST(mpc.Value AS decimal(18,2)) AS Price"
                : @", (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2)) 
               FROM MapperProductCategory m 
               JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €' 
               WHERE m.ProductId = p.Id) AS Price";

            var baseOrder = sort switch
            {
                "cheap" => priceParamId.HasValue ? "TRY_CAST(mpc.Value AS decimal(18,2)) ASC" : "p.CreatedAt DESC",
                "expensive" => priceParamId.HasValue ? "TRY_CAST(mpc.Value AS decimal(18,2)) DESC" : "p.CreatedAt DESC",
                _ => "p.CreatedAt DESC"
            };
            var order = @"CASE WHEN p.PromotionType IN ('Top','Vip') THEN 0 WHEN p.PromotionType = 'Highlight' THEN 1 ELSE 2 END, " + baseOrder;

            var whereClauses = new List<string>();
            int pIdx = 0;

            if (filters != null)
                foreach (var (paramId, values) in filters)
                {
                    if (values is null || values.Count == 0) continue;
                    var fvNames = values.Select((_, i) => $"@fv{pIdx}_{i}").ToList();
                    whereClauses.Add($@"EXISTS (SELECT 1 FROM MapperProductCategory m 
                                        WHERE m.ProductId = p.Id AND m.CategoryId = @fp{pIdx} 
                                        AND m.Value IN ({string.Join(",", fvNames)}))");
                    sqlParams.Add(new SqlParameter($"@fp{pIdx}", paramId));
                    for (int i = 0; i < values.Count; i++) sqlParams.Add(new SqlParameter($"@fv{pIdx}_{i}", values[i]));
                    pIdx++;
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
                    whereClauses.Add($@"EXISTS (SELECT 1 FROM MapperProductCategory m 
                                        WHERE m.ProductId = p.Id AND m.CategoryId = @rp{pIdx} 
                                        AND {string.Join(" AND ", conditions)})");
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

            var extraWhere = whereClauses.Count > 0 ? $"AND (({string.Join(" AND ", whereClauses)}) OR p.PromotionType IN ('Top','Vip'))" : "";

            var catCondition = includeDescendants
                ? "INNER JOIN CategoryClosure cc ON cc.DescendantId = p.CategoryId AND cc.AncestorId = @RootCategoryId"
                : "WHERE p.CategoryId = @RootCategoryId";

            var whereKeyword = includeDescendants ? "WHERE" : "AND";

            var query = $@"SELECT p.Id, p.Name, p.Description, p.Address, p.CreatedAt,
                      COALESCE((SELECT TOP 1 pm.FilePath FROM ProductMedia pm 
                                WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder), p.AvatarUrl) AS AvatarUrl,
                      u.UserName, u.AvatarUrl, u.IsCompany, u.Phone, u.Email, u.CreatedAt, u.Id,
                      p.PromotionType {priceSelect}
                   FROM Products p
                   LEFT JOIN Users u ON u.Id = p.UserId
                   {priceJoin}
                   {catCondition}
                   {whereKeyword} (p.Status = 'active' OR p.Status IS NULL)
                   {extraWhere}
                   ORDER BY {order}";

            return await QueryAsync(query,
                r => new Product
                {
                    Id = r.GetInt32(0),
                    Name = r.GetStringOrDefault(1),
                    Description = r.GetStringOrNull(2),
                    Address = r.GetStringOrNull(3),
                    CreatedAt = r.GetDateTimeOrNull(4),
                    AvatarUrl = r.GetStringOrNull(5),
                    Seller = new UserSummary
                    {
                        Id = r.GetInt32OrDefault(12),
                        UserName = r.GetStringOrNull(6),
                        AvatarUrl = r.GetStringOrNull(7),
                        IsCompany = r.GetBooleanOrDefault(8),
                        Phone = r.GetStringOrNull(9),
                        Email = r.GetStringOrNull(10),
                        CreatedAt = r.GetDateTimeOrNull(11)
                    },
                    Price = r.GetDecimalOrNull(14),
                    PromotionType = r.GetStringOrDefault(13, "None")
                },
                p => { foreach (var sp in sqlParams) p.Add(sp); });
        }
        public async Task<Dictionary<int, string>> GetParamDisplayValuesAsync(int productId, string lang)
        {
            var rawValues = await QueryAsync(
                @"SELECT m.CategoryId,m.Value,c.Type FROM MapperProductCategory m
          JOIN Category c ON c.Id = m.CategoryId WHERE m.ProductId = @ProductId",
                r => (CategoryId: r.GetInt32(0), Value: r.GetStringOrDefault(1), Type: r.GetInt32OrNull(2)),
                p => p.AddWithValue("@ProductId", productId));

            var selectParamIds = rawValues.Where(x => x.Type == 2 || x.Type == 4 || x.Type == 8)
                .Select(x => x.CategoryId).Distinct().ToList();
            var colorParamIds = rawValues.Where(x => x.Type == 6)
                .Select(x => x.CategoryId).Distinct().ToList();

            var options = selectParamIds.Count > 0
                ? await LoadSelectOptionsDictionaryAsync(selectParamIds)
                : new Dictionary<int, (string Value, string ValueLV, string ValueRU)>();
            var colors = colorParamIds.Count > 0
                ? await LoadColorOptionsDictionaryAsync(colorParamIds)
                : new Dictionary<int, (string Name, string NameLV, string NameRU, string Hex)>();

            var result = new Dictionary<int, string>();
            var multiValues = new Dictionary<int, List<string>>();
            foreach (var (paramId, rawValue, type) in rawValues)
            {
                string text;
                if (type == 2 || type == 8) text = ResolveOptionTextFromDictionary(rawValue, options, lang);
                else if (type == 4)
                {
                    var ids = rawValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var texts = ids.Select(id => ResolveOptionTextFromDictionary(id, options, lang));
                    if (!multiValues.ContainsKey(paramId)) multiValues[paramId] = [];
                    multiValues[paramId].AddRange(texts);
                    continue;
                }
                else if (type == 6) text = int.TryParse(rawValue, out int colorId) && colors.TryGetValue(colorId, out var c) ? Resolve(lang, c.Name, c.NameLV, c.NameRU) : rawValue;
                else text = rawValue;
                result[paramId] = text;
            }
            foreach (var (paramId, list) in multiValues) result[paramId] = string.Join(", ", list);
            return result;
        }
        private string ResolveOptionTextFromDictionary(string idStr, Dictionary<int, (string Value, string ValueLV, string ValueRU)> options, string lang)
        {
            if (!int.TryParse(idStr, out int id) || !options.TryGetValue(id, out var texts)) return idStr;
            return Resolve(lang, texts.Value, texts.ValueLV, texts.ValueRU);
        }
        private async Task<Dictionary<int, (string Value, string ValueLV, string ValueRU)>> LoadSelectOptionsDictionaryAsync(List<int> paramIds)
        {
            var (inClause, parameters) = BuildInClause(paramIds, "@pid");
            var data = await QueryAsync(
                $"SELECT Id,Value,ValueLV,ValueRU FROM SelectOptions WHERE IsConf = 1 AND CategoryId IN ({inClause})",
                r => (Id: r.GetInt32(0), Value: r.GetString(1), ValueLV: r.GetStringOrDefault(2, r.GetString(1)), ValueRU: r.GetStringOrDefault(3, r.GetString(1))),
                p => { foreach (var prm in parameters) p.Add(prm); });
            return data.ToDictionary(x => x.Id, x => (x.Value, x.ValueLV, x.ValueRU));
        }
        private async Task<Dictionary<int, (string Name, string NameLV, string NameRU, string Hex)>> LoadColorOptionsDictionaryAsync(List<int> paramIds)
        {
            var (inClause, parameters) = BuildInClause(paramIds, "@pid");
            var data = await QueryAsync(
                $"SELECT Id,Name,NameLV,NameRU,HexValue FROM ColorOptions WHERE IsConf = 1 AND CategoryId IN ({inClause})",
                r => (Id: r.GetInt32(0), Name: r.GetString(1), NameLV: r.GetStringOrDefault(2, r.GetString(1)), NameRU: r.GetStringOrDefault(3, r.GetString(1)), Hex: r.GetString(4)),
                p => { foreach (var prm in parameters) p.Add(prm); });
            return data.ToDictionary(x => x.Id, x => (x.Name, x.NameLV, x.NameRU, x.Hex));
        }
        public async Task<Product?> GetByIdAsync(int id)
        {
            var product = await QuerySingleAsync(@"SELECT p.Id,p.Name,p.Description,p.Address,p.CreatedAt,COALESCE((SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),p.AvatarUrl) AS AvatarUrl,p.CategoryId,p.Status,p.Qty,u.UserName,u.AvatarUrl,u.IsCompany,u.Phone,u.Id,p.UserId,u.Email,u.CreatedAt,p.PromotionType FROM Products p LEFT JOIN Users u ON u.Id = p.UserId WHERE p.Id = @Id", r => new Product { Id = r.GetInt32(0), Name = r.GetStringOrDefault(1), Description = r.GetStringOrNull(2), Address = r.GetStringOrNull(3), CreatedAt = r.GetDateTimeOrNull(4), AvatarUrl = r.GetStringOrNull(5), CategoryId = r.GetInt32OrNull(6), Status = r.IsDBNull(7) ? ProductStatus.Active : Enum.Parse<ProductStatus>(r.GetString(7), true), Qty = r.GetInt32OrNull(8), UserId = r.GetInt32OrNull(14), Seller = new UserSummary { Id = r.GetInt32OrDefault(13), UserName = r.GetStringOrNull(9), AvatarUrl = r.GetStringOrNull(10), IsCompany = r.GetBooleanOrDefault(11), Phone = r.GetStringOrNull(12), Email = r.GetStringOrNull(15), CreatedAt = r.GetDateTimeOrNull(16) }, PromotionType = r.GetStringOrDefault(17, "None") }, p => p.AddWithValue("@Id", id));
            if (product == null) return null;
            var priceVal = await QueryAsync<decimal?>(@"SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2)) FROM MapperProductCategory m JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €' WHERE m.ProductId = @Id", r => r.GetDecimalOrNull(0), p => p.AddWithValue("@Id", id));
            product.Price = priceVal.FirstOrDefault();
            product.Media = await GetMediaAsync(id);
            return product;
        }
        public async Task<List<Product>> GetSimilarAsync(int categoryId, int excludeId, int count = 8) => await QueryAsync(@"SELECT TOP (@Count) p.Id,p.Name,p.Address,p.CreatedAt,COALESCE((SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),p.AvatarUrl) AS AvatarUrl,(SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2)) FROM MapperProductCategory m JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €' WHERE m.ProductId = p.Id) as Price FROM Products p WHERE p.CategoryId = @CatId AND p.Id != @ExcId ORDER BY p.CreatedAt DESC", r => new Product { Id = r.GetInt32(0), Name = r.GetStringOrDefault(1), Address = r.GetStringOrNull(2), CreatedAt = r.GetDateTimeOrNull(3), AvatarUrl = r.GetStringOrNull(4), Price = r.GetDecimalOrNull(5) }, p => { p.AddWithValue("@CatId", categoryId); p.AddWithValue("@ExcId", excludeId); p.AddWithValue("@Count", count); });
        public async Task<List<Product>> GetByUserAsync(int userId, string status = "active") => await QueryAsync(@"SELECT p.Id,p.Name,p.Address,p.CreatedAt,COALESCE((SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),p.AvatarUrl) AS AvatarUrl,p.Status,(SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2)) FROM MapperProductCategory m JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €' WHERE m.ProductId = p.Id) as Price,ISNULL(p.ViewCount,0),(SELECT COUNT(*) FROM Favourites WHERE ProductId = p.Id AND Can = 1) as FavCount,(SELECT COUNT(*) FROM Favourites WHERE ProductId = p.Id AND Can = 0) as CartCount FROM Products p WHERE p.UserId = @UserId AND (p.Status = @Status) ORDER BY p.CreatedAt DESC", r => new Product { Id = r.GetInt32(0), Name = r.GetStringOrDefault(1), Address = r.GetStringOrNull(2), CreatedAt = r.GetDateTimeOrNull(3), AvatarUrl = r.GetStringOrNull(4), Status = r.IsDBNull(5) ? ProductStatus.Active : Enum.Parse<ProductStatus>(r.GetString(5), true), Price = r.GetDecimalOrNull(6), ViewCount = r.GetInt32(7), FavCount = r.GetInt32(8), CartCount = r.GetInt32(9) }, p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@Status", status); });
        public async Task UpdateAsync(Product product, Dictionary<int, string> paramValues, string promotionType = "None")
        {
            var allowedPromotions = new[] { "None", "Highlight", "Top", "Vip" };
            if (!allowedPromotions.Contains(promotionType)) promotionType = "None";
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                await using (var updateCmd = new SqlCommand(@"UPDATE Products SET Name = @Name,Description = @Description,Qty = @Qty,Address = @Address,CategoryId = @CategoryId,PromotionType = @PromotionType WHERE Id = @Id AND UserId = @UserId", conn, tx))
                {
                    updateCmd.Parameters.AddWithValue("@Name", product.Name);
                    updateCmd.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@Qty", (object?)product.Qty ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@Address", (object?)product.Address ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@PromotionType", promotionType);
                    updateCmd.Parameters.AddWithValue("@Id", product.Id);
                    updateCmd.Parameters.AddWithValue("@UserId", product.UserId!);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                await using (var deleteCmd = new SqlCommand("DELETE FROM MapperProductCategory WHERE ProductId = @Id", conn, tx))
                {
                    deleteCmd.Parameters.AddWithValue("@Id", product.Id);
                    await deleteCmd.ExecuteNonQueryAsync();
                }
                var rows = paramValues.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => new object?[] { product.Id, kv.Key, kv.Value });
                await ExecuteBatchInsertAsync(conn, tx, "MapperProductCategory", new[] { "ProductId", "CategoryId", "Value" }, rows);
            });
        }
        public async Task<Dictionary<string, int>> GetCountsByStatusAsync(int userId) => (await QueryAsync("SELECT Status,COUNT(*) FROM Products WHERE UserId = @UserId GROUP BY Status", r => (Status: r.GetString(0), Count: r.GetInt32(1)), p => p.AddWithValue("@UserId", userId))).ToDictionary(x => x.Status, x => x.Count);
        public async Task<Dictionary<int, string>> GetParamValuesAsync(int productId) => (await QueryAsync("SELECT CategoryId,Value FROM MapperProductCategory WHERE ProductId = @ProductId", r => (CategoryId: r.GetInt32(0), Value: r.GetStringOrDefault(1)), p => p.AddWithValue("@ProductId", productId))).ToDictionary(x => x.CategoryId, x => x.Value);
        public async Task<bool> CompleteDealAsync(int productId, int sellerId, int buyerId)
        {
            string imagePath = null;
            int qty = 0;
            decimal? price = 0;
            string delivery = "";
            await using var conn = await OpenConnectionAsync();
            await using var transaction = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                await using (var selectCmd = new SqlCommand(@"SELECT p.AvatarUrl,p.Qty,(SELECT mpc.Value FROM MapperProductCategory mpc INNER JOIN Category c ON mpc.CategoryId = c.Id WHERE mpc.ProductId = p.Id AND c.Name = 'Price, €') AS Price,(SELECT mpc.Value FROM MapperProductCategory mpc INNER JOIN Category c ON mpc.CategoryId = c.Id WHERE mpc.ProductId = p.Id AND c.Name = 'With delivery') AS Delivery FROM Products p WHERE p.Id = @ProductId AND p.UserId = @SellerId AND p.Status = 'Active'", conn, transaction))
                {
                    selectCmd.Parameters.AddWithValue("@ProductId", productId);
                    selectCmd.Parameters.AddWithValue("@SellerId", sellerId);
                    await using (var reader = await selectCmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync()) { await transaction.RollbackAsync(); return false; }
                        imagePath = reader.GetStringOrNull(0);
                        qty = reader.GetInt32(1);
                        price = reader.IsDBNull(2) ? 0 : decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
                        delivery = reader.GetStringOrDefault(3);
                    }
                }
                await using (var updateCmd = new SqlCommand(@"UPDATE Products SET Status = 'Succeeded',AvatarUrl = NULL,ArchivedAt = GETDATE() WHERE Id = @ProductId AND UserId = @SellerId AND Status = 'Active'", conn, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@ProductId", productId);
                    updateCmd.Parameters.AddWithValue("@SellerId", sellerId);
                    var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                    if (rowsAffected == 0) { await transaction.RollbackAsync(); return false; }
                }
                await using (var insertCmd = new SqlCommand(@"INSERT INTO Orders (OrderStatus,CreatedAt,Delivery,Qty,Price,ProductId,UserId) VALUES (@OrderStatus,GETDATE(),@Delivery,@Qty,@Price,@ProductId,@BuyerId)", conn, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@OrderStatus", 1);
                    insertCmd.Parameters.AddWithValue("@Delivery", delivery);
                    insertCmd.Parameters.AddWithValue("@Qty", qty);
                    insertCmd.Parameters.AddWithValue("@Price", price.Value);
                    insertCmd.Parameters.AddWithValue("@ProductId", productId);
                    insertCmd.Parameters.AddWithValue("@BuyerId", buyerId);
                    await insertCmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            if (!string.IsNullOrEmpty(imagePath))
                try { var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath.TrimStart('/')); if (File.Exists(fullPath)) File.Delete(fullPath); } catch (Exception ex) { Console.Error.WriteLine(ex); }
            return true;
        }
        public async Task<bool> ReactivateProductAsync(int productId, int userId) => (await ExecuteAsync(@"UPDATE Products SET Status = 'Active',ArchivedAt = NULL,CreatedAt = SYSUTCDATETIME(),ExpiresAt = DATEADD(day,PublishDurationDays,SYSUTCDATETIME()) WHERE Id = @Id AND UserId = @UserId AND Status = 'Archived'", p => { p.AddWithValue("@Id", productId); p.AddWithValue("@UserId", userId); })) > 0;
        public async Task ArchiveProductsByUserAsync(int userId) => await ExecuteAsync(@"UPDATE Products SET Status = 'Archived',ArchivedAt = GETUTCDATE() WHERE UserId = @UserId AND Status != 'Archived'", p => p.AddWithValue("@UserId", userId));
        public async Task<IEnumerable<Product>> GetUserProductsByStatusAsync(int userId, string status) => await QueryAsync("SELECT * FROM Products WHERE UserId = @UserId AND Status = @Status ORDER BY CreatedAt DESC", r => MapProduct(r), p => { p.AddWithValue("@UserId", userId); p.AddWithValue("@Status", status); });
        private Product MapProduct(SqlDataReader reader) => new Product { Id = reader.GetInt32(reader.GetOrdinal("Id")), UserId = reader.GetInt32(reader.GetOrdinal("UserId")), CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")), Address = reader.GetString(reader.GetOrdinal("Address")), Name = reader.GetString(reader.GetOrdinal("Name")), Description = reader.GetStringOrNull(reader.GetOrdinal("Description")), Price = reader.GetDecimal(reader.GetOrdinal("Price")), AvatarUrl = reader.GetStringOrNull(reader.GetOrdinal("AvatarUrl")), CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")), Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? ProductStatus.Active : Enum.Parse<ProductStatus>(reader.GetString(reader.GetOrdinal("Status"))), ArchivedAt = reader.GetDateTimeOrNull(reader.GetOrdinal("ArchivedAt")) };
        public async Task<bool> UpdateProductStatusAsync(int productId, ProductStatus status)
        {
            int rowsAffected = 0;
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                await using var updateCmd = new SqlCommand("UPDATE Products SET Status = @Status WHERE Id = @Id", conn, tx);
                updateCmd.Parameters.AddWithValue("@Status", status.ToString());
                updateCmd.Parameters.AddWithValue("@Id", productId);
                rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                await using var deleteCmd = new SqlCommand("DELETE FROM Favourites WHERE ProductId = @Id AND Can = 1", conn, tx);
                deleteCmd.Parameters.AddWithValue("@Id", productId);
                await deleteCmd.ExecuteNonQueryAsync();
            });
            return rowsAffected > 0;
        }
        public async Task<int> CreateAsync(Product product, Dictionary<int, string> paramValues, int publishDurationDays = 30, string promotionType = "None")
        {
            if (!new[] { 7, 14, 30, 60, 90 }.Contains(publishDurationDays)) publishDurationDays = 30;
            var allowedPromotions = new[] { "None", "Highlight", "Top", "Vip" };
            if (!allowedPromotions.Contains(promotionType)) promotionType = "None";
            return await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                int newId;
                await using (var insertCmd = new SqlCommand(@"INSERT INTO Products (Name,Description,Qty,Address,CategoryId,UserId,AvatarUrl,CreatedAt,Status,PublishDurationDays,ExpiresAt,PromotionType) OUTPUT INSERTED.Id VALUES (@Name,@Description,@Qty,@Address,@CategoryId,@UserId,@AvatarUrl,GETDATE(),'Active',@Duration,DATEADD(DAY,@Duration,GETDATE()),@PromotionType)", conn, tx))
                {
                    insertCmd.Parameters.AddWithValue("@Name", product.Name);
                    insertCmd.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Qty", (object?)product.Qty ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Address", (object?)product.Address ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@CategoryId", (object?)product.CategoryId ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@UserId", product.UserId!);
                    insertCmd.Parameters.AddWithValue("@AvatarUrl", (object?)product.AvatarUrl ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Duration", publishDurationDays);
                    insertCmd.Parameters.AddWithValue("@PromotionType", promotionType);
                    await using var reader = await insertCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) throw new InvalidOperationException("INSERT INTO Products did not return an Id.");
                    newId = reader.GetInt32(0);
                }
                var rows = paramValues.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => new object?[] { newId, kv.Key, kv.Value });
                await ExecuteBatchInsertAsync(conn, tx, "MapperProductCategory", new[] { "ProductId", "CategoryId", "Value" }, rows);
                return newId;
            });
        }
        public async Task<List<ProductMedia>> GetMediaAsync(int productId) => await QueryAsync("SELECT Id,FilePath,MediaType,SortOrder FROM ProductMedia WHERE ProductId = @Id ORDER BY SortOrder", r => new ProductMedia { Id = r.GetInt32(0), ProductId = productId, FilePath = r.GetString(1), MediaType = r.GetString(2), SortOrder = r.GetInt32(3) }, p => p.AddWithValue("@Id", productId));
        public async Task SaveMediaAsync(int productId, List<ProductMedia> media)
        {
            if (media is null || media.Count == 0) return;
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                var rows = media.Select((m, i) => new object?[] { productId, m.FilePath, m.MediaType, i });
                await ExecuteBatchInsertAsync(conn, tx, "ProductMedia", new[] { "ProductId", "FilePath", "MediaType", "SortOrder" }, rows);
            });
        }
        public async Task DeleteMediaAsync(int productId)
        {
            var paths = await QueryAsync<string>("SELECT FilePath FROM ProductMedia WHERE ProductId = @Id", r => r.GetString(0), p => p.AddWithValue("@Id", productId));
            await ExecuteAsync("DELETE FROM ProductMedia WHERE ProductId = @Id", p => p.AddWithValue("@Id", productId));
            foreach (var path in paths)
                try { var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/')); if (File.Exists(full)) File.Delete(full); } catch (Exception ex) { Console.Error.WriteLine(ex); }
        }
        public async Task IncrementViewCountAsync(int productId) => await ExecuteAsync("UPDATE Products SET ViewCount = ViewCount + 1 WHERE Id = @Id", p => p.AddWithValue("@Id", productId));
        public async Task DeleteAsync(int productId) => await DeleteProductCascade(productId);
        private async Task DeleteProductCascade(int productId)
        {
            var paths = await QueryAsync<string>("SELECT FilePath FROM ProductMedia WHERE ProductId = @Id", r => r.GetString(0), p => p.AddWithValue("@Id", productId));
            var steps = new[] { "DELETE FROM ProductMedia WHERE ProductId = @Id", "DELETE FROM MapperProductCategory WHERE ProductId = @Id", "DELETE FROM Favourites WHERE ProductId = @Id", "DELETE FROM Reports WHERE ProductId = @Id", "DELETE FROM Notifications WHERE ProductId = @Id", "DELETE FROM Messages WHERE ConversationId IN (SELECT Id FROM Conversations WHERE ProductId = @Id)", "DELETE FROM Conversations WHERE ProductId = @Id", "DELETE FROM Reviews WHERE ProductId = @Id", "DELETE FROM Products WHERE Id = @Id" };
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                foreach (var sql in steps)
                {
                    await using var cmd = new SqlCommand(sql, conn, tx);
                    cmd.Parameters.AddWithValue("@Id", productId);
                    await cmd.ExecuteNonQueryAsync();
                }
            });
            foreach (var path in paths)
                try { var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/')); if (File.Exists(full)) File.Delete(full); } catch (Exception ex) { Console.Error.WriteLine(ex); }
        }
        public async Task<(List<AdminConfOptionRow> Items, int TotalCount)> GetUnconfirmedOptionsAsync()
        {
            var items = new List<AdminConfOptionRow>();
            int totalCount = 0;
            await using var conn = await OpenConnectionAsync();
            var sql = @"SELECT COUNT(*) FROM dbo.SelectOptions so INNER JOIN dbo.MapperProductCategory mpc ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%' INNER JOIN dbo.Products p ON mpc.ProductId = p.Id WHERE so.IsConf = 0;SELECT so.Id AS OptionId,so.Value AS OptionValue,so.ValueLV AS OptionValueLV,so.ValueRU AS OptionValueRU,p.Id AS ProductId,p.Name AS ProductName,p.CreatedAt AS CreatedAt,u.Id AS UserId,u.UserName AS UserName,c.Id AS CategoryId,c.Name AS CategoryName,c2.Name AS OptionCategory,c.NameLV AS CategoryNameLV,c2.NameLV AS OptionCategoryLV,c.NameRU AS CategoryNameRU,c2.NameRU AS OptionCategoryRU FROM dbo.SelectOptions so INNER JOIN dbo.MapperProductCategory mpc ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%' INNER JOIN dbo.Products p ON mpc.ProductId = p.Id INNER JOIN dbo.Users u ON p.UserId = u.Id INNER JOIN dbo.Category c ON p.CategoryId = c.Id INNER JOIN dbo.Category c2 ON so.CategoryId = c2.Id WHERE so.IsConf = 0;";
            await using var cmd = new SqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) totalCount = r.GetInt32(0);
            await r.NextResultAsync();
            while (await r.ReadAsync())
                items.Add(new AdminConfOptionRow { OptionId = r.GetInt32(0), OptionValue = r.GetStringOrDefault(1), OptionValueLV = r.GetStringOrDefault(2), OptionValueRU = r.GetStringOrDefault(3), ProductId = r.GetInt32(4), ProductName = r.GetStringOrDefault(5), CreatedAt = r.GetDateTimeOrNull(6), UserId = r.GetInt32(7), UserName = r.GetStringOrDefault(8), CategoryId = r.GetInt32(9), CategoryName = r.GetStringOrDefault(10), ParameterName = r.GetStringOrDefault(11), CategoryNameLV = r.GetStringOrDefault(12), ParameterNameLV = r.GetStringOrDefault(13), CategoryNameRU = r.GetStringOrDefault(14), ParameterNameRU = r.GetStringOrDefault(15) });
            return (items, totalCount);
        }
        public async Task<bool> ApproveSelectOptionAsync(int optionId) => (await ExecuteAsync("UPDATE SelectOptions SET IsConf = 1 WHERE Id = @Id", p => p.AddWithValue("@Id", optionId))) > 0;
        public async Task<bool> RejectProductAndOptionAsync(int optionId, int productId)
        {
            try
            {
                await ExecuteInTransactionAsync(async (conn, tx) =>
                {
                    await using var productCmd = new SqlCommand("UPDATE Products SET Status = 'Rejected' WHERE Id = @ProductId", conn, tx);
                    productCmd.Parameters.AddWithValue("@ProductId", productId);
                    await productCmd.ExecuteNonQueryAsync();
                    await using var optionCmd = new SqlCommand("DELETE FROM SelectOptions WHERE Id = @OptionId", conn, tx);
                    optionCmd.Parameters.AddWithValue("@OptionId", optionId);
                    await optionCmd.ExecuteNonQueryAsync();
                });
                return true;
            }
            catch { return false; }
        }
        public async Task<int?> GetPriceParamIdAsync(int productId) => (await QueryAsync<int?>(@"SELECT TOP 1 mpc.CategoryId FROM MapperProductCategory mpc JOIN Category c ON c.Id = mpc.CategoryId AND c.Name = 'Price, €' WHERE mpc.ProductId = @Id", r => r.GetInt32OrNull(0), p => p.AddWithValue("@Id", productId))).FirstOrDefault();
        public async Task<int> RecalculateModerationScoreAsync(int productId)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(@";WITH UnconfirmedCount AS (SELECT COUNT(*) AS Cnt FROM MapperProductCategory mpc JOIN SelectOptions so ON TRY_CAST(mpc.Value AS int) = so.Id JOIN Category c ON c.Id = mpc.CategoryId AND c.Type IN (2,4,8) WHERE mpc.ProductId = @Id AND so.IsConf = 0) UPDATE p SET ModerationScore = (SELECT Cnt FROM UnconfirmedCount),Status = CASE WHEN (SELECT Cnt FROM UnconfirmedCount) >= 5 THEN 'Moderation' ELSE Status END OUTPUT INSERTED.ModerationScore FROM Products p WHERE p.Id = @Id;", conn);
            cmd.Parameters.AddWithValue("@Id", productId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync()) return reader.GetInt32(0);
            return 0;
        }
        public async Task<List<int>> GetFavouriteSubscriberIdsAsync(int productId, int excludeUserId) => await QueryAsync("SELECT DISTINCT UserId FROM Favourites WHERE ProductId = @ProductId AND Can = 1 AND UserId != @ExcludeUserId", r => r.GetInt32(0), p => { p.AddWithValue("@ProductId", productId); p.AddWithValue("@ExcludeUserId", excludeUserId); });
        public async Task<List<Product>> GetPurchasedByUserAsync(int userId) => await QueryAsync(@"SELECT p.Id,p.Name,p.Address,p.CreatedAt,COALESCE((SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),p.AvatarUrl) AS AvatarUrl,p.Status,o.Price AS Price FROM Products p INNER JOIN (SELECT ProductId,Price,CreatedAt,ROW_NUMBER() OVER (PARTITION BY ProductId ORDER BY CreatedAt DESC) AS rn FROM Orders WHERE UserId = @UserId AND OrderStatus = 1) o ON o.ProductId = p.Id AND o.rn = 1 ORDER BY o.CreatedAt DESC", r => new Product { Id = r.GetInt32(0), Name = r.GetStringOrDefault(1), Address = r.GetStringOrNull(2), CreatedAt = r.GetDateTimeOrNull(3), AvatarUrl = r.GetStringOrNull(4), Status = ProductStatus.Succeeded, Price = r.GetDecimalOrNull(6) }, p => p.AddWithValue("@UserId", userId));
        public async Task<int> GetPurchasedConversationCountAsync(int userId) => (await QueryAsync<int>(@"SELECT COUNT(DISTINCT p.Id) FROM Products p JOIN Conversations conv ON conv.ProductId = p.Id AND conv.IsSystem = 1 AND conv.BuyerId = @UserId WHERE p.Status = 'Succeeded'", r => r.GetInt32(0), p => p.AddWithValue("@UserId", userId)))[0];
        public async Task DeleteSpecificMediaAsync(IEnumerable<int> mediaIds)
        {
            var ids = mediaIds.ToList();
            if (ids.Count == 0) return;
            const int chunkSize = 2000;
            var paths = new List<string>();
            foreach (var chunk in ids.Chunk(chunkSize))
            {
                var (sql, parameters) = BuildInClause(chunk, "@id");
                paths.AddRange(await QueryAsync<string>($"SELECT FilePath FROM ProductMedia WHERE Id IN ({sql})", r => r.GetString(0), p => { foreach (var prm in parameters) p.Add(prm); }));
            }
            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                foreach (var chunk in ids.Chunk(chunkSize))
                {
                    var (sql, parameters) = BuildInClause(chunk, "@id");
                    await using var cmd = new SqlCommand($"DELETE FROM ProductMedia WHERE Id IN ({sql})", conn, tx);
                    foreach (var prm in parameters) cmd.Parameters.Add(prm);
                    await cmd.ExecuteNonQueryAsync();
                }
            });
            foreach (var path in paths)
                try { var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/')); if (File.Exists(full)) File.Delete(full); } catch (Exception ex) { Console.Error.WriteLine(ex); }
        }
        public async Task UpdatePublishDurationAsync(int productId, int userId, int days) => await ExecuteAsync(@"UPDATE Products SET PublishDurationDays = @D,ExpiresAt = DATEADD(DAY,@D,GETDATE()) WHERE Id = @Id AND UserId = @UserId", p => { p.AddWithValue("@D", days); p.AddWithValue("@Id", productId); p.AddWithValue("@UserId", userId); });
        public async Task<List<int>> GetSubscriberIdsExcludingAsync(int sellerId, int excludeBuyerId) => await QueryAsync("SELECT FollowerId FROM Subscriptions WHERE FollowingId = @SellerId AND FollowerId != @BuyerId", r => r.GetInt32(0), p => { p.AddWithValue("@SellerId", sellerId); p.AddWithValue("@BuyerId", excludeBuyerId); });
    }
}