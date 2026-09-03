using Microsoft.Extensions.Caching.Memory;

namespace Linkora.Repositories
{
    public class SelectOptionRepository : SqlRepositoryBase, ISelectOptionRepository
    {
        private readonly IMemoryCache _cache;
        public SelectOptionRepository(IConfiguration configuration, IMemoryCache cache) : base(configuration)
        {
            _cache = cache;
        }
        private static string ValueColumn(string lang) => lang switch
        {
            "lv" => "ValueLV",
            "ru" => "ValueRU",
            _ => "Value"
        };
        public async Task<int?> FindIdAsync(int paramId, string text, string lang) => (await QueryAsync<int?>(
                $@"SELECT Id FROM SelectOptions
                   WHERE ParamId = @ParamId
                     AND LTRIM(RTRIM({ValueColumn(lang)})) = LTRIM(RTRIM(@Text))",
                r => r.GetInt32OrNull(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", text.Trim());
                })).FirstOrDefault();
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
        public async Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang)
        {
            string cacheKey = $"select_options_{paramId}_{lang}";

            if (_cache.TryGetValue(cacheKey, out List<(int Id, string Text)>? cached) && cached != null) return cached;

            var result = await QueryAsync<(int Id, string Text)>($@"SELECT Id, {ValueColumn(lang)} FROM SelectOptions WHERE ParamId = @ParamId AND IsConf = 1",
                                                                    r => (r.GetInt32(0), r.GetStringOrDefault(1)), p => p.AddWithValue("@ParamId", paramId));

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));

            return result;
        }
        private void InvalidateCache(int paramId, string lang)
        {
            _cache.Remove($"select_options_{paramId}_{lang}");
        }
    }
}