using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

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
        private void InvalidateCache(int paramId, string lang)
        {
            _cache.Remove($"select_options_{paramId}_{lang}");
        }
    }
}