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
        public async Task<int?> FindIdAsync(int paramId, string text, string lang)
        {
            var col = ValueColumn(lang);
            var trimmed = text.Trim();

            var result = await QueryAsync<int?>(
                $@"SELECT Id FROM SelectOptions
                   WHERE CategoryId = @ParamId
                     AND LTRIM(RTRIM({col})) = LTRIM(RTRIM(@Text))",
                r => r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", trimmed);
                });

            return result.FirstOrDefault();
        }
        public async Task<int> CreateAsync(int paramId, string text)
        {
            var trimmed = text.Trim();

            var result = await QueryAsync<int>(
                @"INSERT INTO SelectOptions (CategoryId, Value, ValueLV, ValueRU, IsConf)
                  OUTPUT INSERTED.Id
                  VALUES (@ParamId, @Text, @Text, @Text, 0)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@ParamId", paramId);
                    p.AddWithValue("@Text", trimmed);
                });

            return result[0];
        }
        public async Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang)
        {
            var col = ValueColumn(lang);

            return await QueryAsync<(int Id, string Text)>(
                $@"SELECT Id, {col}
                   FROM SelectOptions
                   WHERE CategoryId = @ParamId and IsConf = 1",
                r => (r.GetInt32(0), r.IsDBNull(1) ? string.Empty : r.GetString(1)),
                p => p.AddWithValue("@ParamId", paramId));
        }
    }
}