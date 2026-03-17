using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class AddressRepository : IAddressRepository
    {
        private readonly string _connectionString;

        public AddressRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<(int Id, string Name)>> GetCitiesAsync()
            => await QueryAsync("SELECT Id, Name FROM Cities ORDER BY Name");

        public async Task<List<(int Id, string Name)>> GetStreetsAsync(int cityId)
            => await QueryAsync("SELECT Id, Name FROM Streets WHERE CityId = @P ORDER BY Name", cityId);

        public async Task<List<(int Id, string Name)>> GetHousesAsync(int streetId)
            => await QueryAsync("SELECT Id, Name FROM Houses WHERE StreetId = @P ORDER BY Name", streetId);

        private async Task<List<(int, string)>> QueryAsync(string sql, int? param = null)
        {
            var result = new List<(int, string)>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            if (param.HasValue) cmd.Parameters.AddWithValue("@P", param.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add((r.GetInt32(0), r.GetString(1)));
            return result;
        }
    }
}