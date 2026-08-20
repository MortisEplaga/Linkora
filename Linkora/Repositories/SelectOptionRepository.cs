using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class SelectOptionRepository : ISelectOptionRepository
    {
        private readonly string _connectionString;

        public SelectOptionRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

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

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand($@"
                SELECT Id FROM SelectOptions
                WHERE CategoryId = @ParamId
                  AND LTRIM(RTRIM({col})) = LTRIM(RTRIM(@Text))", conn);
            cmd.Parameters.AddWithValue("@ParamId", paramId);
            cmd.Parameters.AddWithValue("@Text", trimmed);
            var existingId = await cmd.ExecuteScalarAsync();

            return existingId == null ? null : (int)existingId;
        }
        public async Task<int> CreateAsync(int paramId, string text)
        {
            var trimmed = text.Trim();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                INSERT INTO SelectOptions (CategoryId, Value, ValueLV, ValueRU, IsConf)
                OUTPUT INSERTED.Id
                VALUES (@ParamId, @Text, @Text, @Text, 0)", conn);
            cmd.Parameters.AddWithValue("@ParamId", paramId);
            cmd.Parameters.AddWithValue("@Text", trimmed);

            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        public async Task<List<(int Id, string Text)>> GetConfirmedAsync(int paramId, string lang)
        {
            var col = ValueColumn(lang);
            var result = new List<(int, string)>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand($@"
                SELECT Id, {col}
                FROM SelectOptions
                WHERE CategoryId = @ParamId and IsConf = 1", conn);
            cmd.Parameters.AddWithValue("@ParamId", paramId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add((reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));

            return result;
        }
    }
}