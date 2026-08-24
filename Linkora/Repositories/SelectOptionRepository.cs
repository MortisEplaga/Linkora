namespace Linkora.Repositories
{
    public class SelectOptionRepository : SqlRepositoryBase, ISelectOptionRepository
    {
        public SelectOptionRepository(IConfiguration configuration) : base(configuration) { }
        private static string ValueColumn(string lang) => lang switch
        {
            "lv" => "ValueLV",
            "ru" => "ValueRU",
            _ => "Value"
        };
        public async Task<int?> FindIdAsync(int paramId, string text, string lang) => (await QueryAsync<int?>(
                $@"SELECT Id FROM SelectOptions
                   WHERE CategoryId = @ParamId
                     AND LTRIM(RTRIM({ValueColumn(lang)})) = LTRIM(RTRIM(@Text))",
                r => r.GetInt32OrNull(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", text.Trim());
                })).FirstOrDefault();
        public async Task<int> CreateAsync(int paramId, string text) => (await QueryAsync<int>(
                @"INSERT INTO SelectOptions (CategoryId, Value, ValueLV, ValueRU, IsConf)
                  OUTPUT INSERTED.Id
                  VALUES (@ParamId, @Text, @Text, @Text, 0)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", text.Trim());
                }))[0];
        public async Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang) => await QueryAsync<(int Id, string Text)>(
                $@"SELECT Id, {ValueColumn(lang)}
                   FROM SelectOptions
                   WHERE CategoryId = @ParamId and IsConf = 1",
                r => (r.GetInt32(0), r.GetStringOrDefault(1)),
                p => p.AddWithValue("@ParamId", paramId));
    }
}