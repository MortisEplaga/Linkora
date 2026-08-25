using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class CategoryRepository : SqlRepositoryBase, ICategoryRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public CategoryRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : base(configuration)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        private Category MapRow(SqlDataReader reader)
        {
            var nameEn = reader.GetString(reader.GetOrdinal("Name"));
            return new Category
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ParentId = reader.GetInt32OrNull(reader.GetOrdinal("ParentId")),
                Name = Resolve(_httpContextAccessor.HttpContext.GetLang(), nameEn, reader.GetStringOrNull(reader.GetOrdinal("NameLV")), reader.GetStringOrNull(reader.GetOrdinal("NameRU"))),
                NameEn = nameEn,
                Type = reader.GetInt32OrNull(reader.GetOrdinal("Type")),
            };
        }
        public async Task<List<Category>> GetAllAsync() => await QueryAsync("SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category", MapRow);
        public async Task<Category?> GetByIdAsync(int id) => await QuerySingleAsync("SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE Id = @Id and Type = 1", MapRow, p => p.AddWithValue("@Id", id));
        public async Task<List<Category>> GetChildrenAsync(int parentId) => await QueryAsync("SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId = @ParentId and Type = 1", MapRow, p => p.AddWithValue("@ParentId", parentId));
        public async Task<List<Category>> GetBreadcrumbAsync(int rootCategoryId, bool includeSelf = false) => await QueryAsync(@"SELECT c.Id, c.ParentId, c.Name, c.Type, c.NameLV, c.NameRU
                                                                                                                                 FROM CategoryClosure cc
                                                                                                                                 INNER JOIN Category c ON c.Id = cc.AncestorId
                                                                                                                                 WHERE cc.DescendantId = @RootId AND c.Type = 1
                                                                                                                                 ORDER BY cc.Depth DESC", MapRow, p => {p.AddWithValue("@RootId", rootCategoryId);});
        public async Task<List<Parameter>> GetParametersAsync(IEnumerable<int> categoryIds)
        {
            var ids = string.Join(",", categoryIds);
            if (string.IsNullOrEmpty(ids)) return new List<Parameter>();
            return await LoadParameterOptionsAsync(await QueryAsync($"SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId IN ({ids}) AND Type IN (2,3,4,5,6,7,8)", MapRow));
        }
        public async Task<List<Parameter>> GetParametersAsync(int categoryId) => await LoadParameterOptionsAsync(await QueryAsync(
                "SELECT Id, ParentId, Name, Type, NameLV, NameRU FROM Category WHERE ParentId = @ParentId AND Type IN (2,3,4,5,6,7,8)",
                MapRow, p => p.AddWithValue("@ParentId", categoryId)));
        private async Task<List<Parameter>> LoadParameterOptionsAsync(List<Category> parameters)
        {
            if (parameters == null || parameters.Count == 0) return new List<Parameter>();

            var lang = _httpContextAccessor.HttpContext.GetLang();

            var resultDict = parameters.ToDictionary(p => p.Id, p => new Parameter { Param = p });

            (string InClause, Action<SqlParameterCollection> AddParams) PrepareInClause(List<int> ids)
            {
                var paramNames = ids.Select((_, i) => $"@id{i}").ToList();
                var inClause = string.Join(",", paramNames);

                Action<SqlParameterCollection> addParams = pr =>
                {
                    for (int i = 0; i < ids.Count; i++) pr.AddWithValue($"@id{i}", ids[i]);
                };

                return (inClause, addParams);
            }

            var selectIds = parameters.Where(p => p.Type == 2 || p.Type == 4 || p.Type == 8).Select(p => p.Id).ToList();
            if (selectIds.Count > 0)
            {
                var (inClause, addParams) = PrepareInClause(selectIds);

                var options = await QueryAsync(
                    $"SELECT CategoryId, Id, Value, ValueLV, ValueRU FROM SelectOptions WHERE CategoryId IN ({inClause}) AND IsConf = 1",
                    r => new
                    {
                        CategoryId = r.GetInt32(0),
                        Option = new SelectOption
                        {
                            Id = r.GetInt32(1),
                            Text = Resolve(lang, r.GetString(2), r.GetStringOrNull(3), r.GetStringOrNull(4))
                        }
                    },
                    addParams);

                foreach (var item in options) if (resultDict.TryGetValue(item.CategoryId, out var param)) param.Options.Add(item.Option);
            }

            var rangeIds = parameters.Where(p => p.Type == 5).Select(p => p.Id).ToList();
            if (rangeIds.Count > 0)
            {
                var (inClause, addParams) = PrepareInClause(rangeIds);

                var ranges = await QueryAsync(
                    $"SELECT ParamId, MinValue, MaxValue, Step FROM ParameterRange WHERE ParamId IN ({inClause})",
                    r => new
                    {
                        ParamId = r.GetInt32(0),
                        Min = r.GetDecimalOrNull(1),
                        Max = r.GetDecimalOrNull(2),
                        Step = r.GetDecimalOrNull(3)
                    },
                    addParams);

                foreach (var range in ranges)
                    if (resultDict.TryGetValue(range.ParamId, out var param))
                    {
                        param.Min = range.Min;
                        param.Max = range.Max;
                        param.Step = range.Step;
                    }
            }

            var colorIds = parameters.Where(p => p.Type == 6).Select(p => p.Id).ToList();
            if (colorIds.Count > 0)
            {
                var (inClause, addParams) = PrepareInClause(colorIds);

                var colors = await QueryAsync(
                    $"SELECT CategoryId, Id, Name, NameLV, NameRU, HexValue, IsMain FROM ColorOptions WHERE CategoryId IN ({inClause}) AND IsConf = 1 ORDER BY IsMain DESC",
                    r => new
                    {
                        CategoryId = r.GetInt32(0),
                        Option = new ColorOption
                        {
                            Id = r.GetInt32(1),
                            Name = Resolve(lang, r.GetString(2), r.GetStringOrNull(3), r.GetStringOrNull(4)),
                            HexValue = r.GetString(5),
                            IsMain = r.GetBoolean(6),
                        }
                    },
                    addParams);

                foreach (var item in colors) if (resultDict.TryGetValue(item.CategoryId, out var param)) param.ColorOptions.Add(item.Option);
            }

            return resultDict.Values.ToList();
        }
        public async Task RebuildClosureAsync()
        {
            await ExecuteAsync("DELETE FROM CategoryClosure");
            await ExecuteAsync(@";WITH CatTree AS (
                                    SELECT Id AS AncestorId, Id AS DescendantId, 0 AS Depth FROM Category
                                    UNION ALL
                                    SELECT ct.AncestorId, c.Id, ct.Depth + 1
                                    FROM Category c
                                    JOIN CatTree ct ON c.ParentId = ct.DescendantId
                                )
                                INSERT INTO CategoryClosure (AncestorId, DescendantId, Depth)
                                SELECT AncestorId, DescendantId, Depth FROM CatTree");
        }
    }
}