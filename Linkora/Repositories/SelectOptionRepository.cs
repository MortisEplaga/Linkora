using Linkora.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace Linkora.Repositories
{
    public class SelectOptionRepository : SqlRepositoryBase, ISelectOptionRepository
    {
        private readonly IMemoryCache _cache;
        public SelectOptionRepository(IConfiguration configuration, IMemoryCache cache) : base(configuration) { _cache = cache; }
        private static string ValueColumn(string lang) => lang switch
        {
            "lv" => "ValueLV",
            "ru" => "ValueRU",
            _ => "Value"
        };
        public async Task<int> CreateAsync(int paramId, string text) => (await QueryAsync<int>(
                @"INSERT INTO SelectOptions (ParamId, Value, ValueLV, ValueRU, IsConf)
                  OUTPUT INSERTED.Id
                  VALUES (@ParamId, @Text, @Text, @Text, 0)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", text.Trim());
                }))[0];
        public async Task<int> CreateAsync(SqlConnection conn, SqlTransaction tx, int paramId, string text)
        {
            await using var cmd = new SqlCommand(@"INSERT INTO SelectOptions (ParamId, Value, ValueLV, ValueRU, IsConf) OUTPUT INSERTED.Id VALUES (@ParamId, @Text, @Text, @Text, 0)", conn, tx);
            cmd.Parameters.AddWithValue("@ParamId", paramId);
            cmd.Parameters.AddWithValue("@Text", text.Trim());
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        public async Task<int?> FindIdAsync(int paramId, string text, string lang, bool includeFilterOnly = false) => (await QueryAsync<int?>(
                $@"SELECT Id FROM SelectOptions WHERE ParamId = @ParamId AND LTRIM(RTRIM({ValueColumn(lang)})) = LTRIM(RTRIM(@Text))
             {(includeFilterOnly ? "" : "AND FilterOnly = 0")}",
                r => r.GetInt32OrNull(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", text.Trim());
                })).FirstOrDefault();
        public async Task<int?> FindIdAsync(SqlConnection conn, SqlTransaction tx, int paramId, string text, string lang)
        {
            await using var cmd = new SqlCommand($@"SELECT Id FROM SelectOptions WHERE ParamId = @ParamId AND FilterOnly = 0 AND LTRIM(RTRIM({ValueColumn(lang)})) = LTRIM(RTRIM(@Text))", conn, tx);
            cmd.Parameters.AddWithValue("@ParamId", paramId);
            cmd.Parameters.AddWithValue("@Text", text.Trim());
            var scalar = await cmd.ExecuteScalarAsync();
            return scalar is int id ? id : null;
        }
        public async Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang, bool forFilter = false)
        {
            string cacheKey = $"select_options_{paramId}_{lang}_{(forFilter ? "f" : "p")}";

            if (_cache.TryGetValue(cacheKey, out List<(int Id, string Text)>? cached) && cached != null) return cached;

            var result = await QueryAsync<(int Id, string Text)>($@"SELECT Id, {ValueColumn(lang)} FROM SelectOptions WHERE ParamId = @ParamId AND IsConf = 1 {(forFilter ? "" : "AND FilterOnly = 0")}",
                r => (r.GetInt32(0), r.GetStringOrDefault(1)), p => p.AddWithValue("@ParamId", paramId));

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }
        public async Task<Dictionary<int, (string Value, string ValueLV, string ValueRU)>> GetConfirmedTextsAsync(IEnumerable<int> paramIds)
        {
            var (inClause, prms) = BuildInClause(paramIds, "@pid");
            var rows = await QueryAsync($"SELECT Id, Value, ValueLV, ValueRU FROM SelectOptions WHERE ParamId IN ({inClause}) AND IsConf = 1 AND FilterOnly = 0",
                r => (Id: r.GetInt32(0), Value: r.GetStringOrDefault(1), ValueLV: r.GetStringOrDefault(2), ValueRU: r.GetStringOrDefault(3)),
                p => { foreach (var prm in prms) p.Add(prm); });
            return rows.ToDictionary(x => x.Id, x => (x.Value, x.ValueLV, x.ValueRU));
        }
        public async Task<List<OptionRelationDto>> GetRelationsAsync(IEnumerable<int>? categoryIds = null, int? parentParamId = null, int? childParamId = null)
        {
            var idList = (categoryIds ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();
            var cacheKey = $"select_option_relations_{parentParamId}_{childParamId}_{string.Join(",", idList)}";

            if (_cache.TryGetValue(cacheKey, out List<OptionRelationDto>? cached) && cached != null) return cached;

            var sql = new StringBuilder(@"SELECT po.ParamId, co.ParamId, r.ParentOptionId, r.ChildOptionId
FROM SelectOptionRelations r
JOIN SelectOptions po ON po.Id = r.ParentOptionId AND po.IsConf = 1
JOIN SelectOptions co ON co.Id = r.ChildOptionId AND co.IsConf = 1
WHERE (@ParentParamId IS NULL OR po.ParamId = @ParentParamId)
  AND (@ChildParamId IS NULL OR co.ParamId = @ChildParamId)");

            if (idList.Count > 0)
            {
                var ids = string.Join(",", idList);
                sql.Append($" AND (po.ParamId IN (SELECT Id FROM Parameters WHERE CategoryId IN ({ids}))")
                   .Append($" OR co.ParamId IN (SELECT Id FROM Parameters WHERE CategoryId IN ({ids})))");
            }

            var result = await QueryAsync(sql.ToString(),
                r => new OptionRelationDto
                {
                    ParentParamId = r.GetInt32(0),
                    ChildParamId = r.GetInt32(1),
                    ParentOptionId = r.GetInt32(2),
                    ChildOptionId = r.GetInt32(3)
                },
                p =>
                {
                    p.AddWithValue("@ParentParamId", (object?)parentParamId ?? DBNull.Value);
                    p.AddWithValue("@ChildParamId", (object?)childParamId ?? DBNull.Value);
                });

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }
        private void InvalidateCache(int paramId, string lang)
        {
            _cache.Remove($"select_options_{paramId}_{lang}");
        }
    }
}