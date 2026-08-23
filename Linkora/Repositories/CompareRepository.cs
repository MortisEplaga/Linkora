using Linkora.Models;

namespace Linkora.Repositories
{
    public class CompareRepository : SqlRepositoryBase, ICompareRepository
    {
        public CompareRepository(IConfiguration config) : base(config) { }
        public async Task<CompareData> GetCompareDataAsync(int userId, string lang)
        {
            var result = new CompareData();

            var products = await QueryAsync(
                @"SELECT p.Id, p.Name, p.Address, p.CreatedAt,
                       COALESCE(
                           (SELECT TOP 1 pm.FilePath FROM ProductMedia pm
                            WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                           p.AvatarUrl
                       ) AS AvatarUrl,
                       (SELECT COUNT(*) FROM ProductMedia pm2 WHERE pm2.ProductId = p.Id) AS MediaCount,
                       (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                        FROM MapperProductCategory m
                        JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                        WHERE m.ProductId = p.Id) AS Price,
                        cat.Name AS CategoryName, cat.NameLV AS CategoryNameLV, cat.NameRU AS CategoryNameRU,
                        u.UserName
                  FROM Favourites f
                  JOIN Products p ON p.Id = f.ProductId
                  LEFT JOIN Category cat ON cat.Id = p.CategoryId
                  LEFT JOIN Users u ON u.Id = p.UserId
                  WHERE f.UserId = @U AND f.Can = 0
                  ORDER BY f.Id",
                r =>
                {
                    var catNameEn = r.GetStringOrDefault(7);
                    return new CompareProduct
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetStringOrDefault(1),
                        Address = r.GetStringOrNull(2),
                        CreatedAt = r.GetDateTimeOrNull(3),
                        AvatarUrl = r.GetStringOrNull(4),
                        MediaCount = r.GetInt32OrDefault(5),
                        Price = r.GetDecimalOrNull(6),
                        CategoryName = Resolve(lang, catNameEn, r.IsDBNull(8) ? catNameEn : r.GetString(8), r.IsDBNull(9) ? catNameEn : r.GetString(9)),
                        SellerName = r.GetStringOrNull(10),
                    };
                },
                p => p.AddWithValue("@U", userId));

            result.Products.AddRange(products);

            if (result.Products.Count == 0) return result;

            var selectOptionsList = await QueryAsync(
                "SELECT Id, Value, ValueLV, ValueRU FROM SelectOptions WHERE IsConf = 1",
                r =>
                {
                    var value = r.GetString(1);
                    return (Id: r.GetInt32(0), Value: value, ValueLV: r.IsDBNull(2) ? value : r.GetString(2), ValueRU: r.IsDBNull(3) ? value : r.GetString(3));
                });

            var selectOptionsDict = selectOptionsList.ToDictionary(x => x.Id, x => (x.Value, x.ValueLV, x.ValueRU));

            var productIds = result.Products.Select(p => p.Id).ToList();
            var idParams = productIds.Select((id, i) => $"@p{i}").ToArray();
            var inClause = string.Join(",", idParams);

            var paramRows = await QueryAsync(
                $@"SELECT mpc.ProductId, c.Id AS ParamId, c.Name, c.NameLV, c.NameRU, c.Type, mpc.Value,
                          co.Name AS ColorName, co.NameLV AS ColorNameLV, co.NameRU AS ColorNameRU
                   FROM MapperProductCategory mpc
                   JOIN Category c ON c.Id = mpc.CategoryId
                   LEFT JOIN ColorOptions co ON c.Type = 6 AND TRY_CAST(mpc.Value AS int) = co.Id
                   WHERE mpc.ProductId IN ({inClause})
                     AND c.Name != 'Price, €'
                     AND c.Type IN (2,3,4,5,6,7,8)
                   ORDER BY c.Name",
                r => (
                    ProductId: r.GetInt32(0),
                    ParamId: r.GetInt32(1),
                    NameEn: r.GetString(2),
                    NameLv: r.IsDBNull(3) ? r.GetString(2) : r.GetString(3),
                    NameRu: r.IsDBNull(4) ? r.GetString(2) : r.GetString(4),
                    ParamType: r.GetInt32OrNull(5),
                    RawValue: r.GetStringOrDefault(6),
                    ColorNameEn: r.GetStringOrNull(7),
                    ColorNameLv: r.GetStringOrNull(8),
                    ColorNameRu: r.GetStringOrNull(9)
                ),
                p =>
                {
                    for (int i = 0; i < productIds.Count; i++)
                        p.AddWithValue($"@p{i}", productIds[i]);
                });

            foreach (var row in paramRows)
            {
                result.ParamLabels[row.ParamId] = Resolve(lang, row.NameEn, row.NameLv, row.NameRu);

                var rawValue = row.RawValue;
                string value;

                if (row.ParamType == 4)
                {
                    var ids = rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var texts = new List<string>();
                    foreach (var idStr in ids)
                        if (int.TryParse(idStr.Trim(), out int optId) && selectOptionsDict.TryGetValue(optId, out var textsTuple))
                            texts.Add(Resolve(lang, textsTuple.Value, textsTuple.ValueLV, textsTuple.ValueRU));
                        else texts.Add(idStr);

                    value = string.Join(", ", texts);
                }
                else if (row.ParamType == 2 || row.ParamType == 8)
                    if (int.TryParse(rawValue, out int optId) && selectOptionsDict.TryGetValue(optId, out var textsTuple))
                        value = Resolve(lang, textsTuple.Value, textsTuple.ValueLV, textsTuple.ValueRU);
                    else value = rawValue;
                else if (row.ParamType == 6)
                    if (row.ColorNameEn == null) value = rawValue;
                    else
                    {
                        if (lang == "lv" && row.ColorNameLv != null) value = row.ColorNameLv;
                        else if (lang == "ru" && row.ColorNameRu != null) value = row.ColorNameRu;
                        else value = row.ColorNameEn;
                    }
                else value = rawValue;

                if (!result.ParamMatrix.ContainsKey(row.ParamId))
                    result.ParamMatrix[row.ParamId] = [];

                result.ParamMatrix[row.ParamId][row.ProductId] = value;
            }

            result.AllParamIds = result.ParamMatrix.Keys.OrderBy(id => result.ParamLabels[id]).ToList();
            return result;
        }
    }
}