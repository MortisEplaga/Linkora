using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ParcelLockerRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), IParcelLockerRepository
    {
        private const string Columns = "Id, Provider, Code, Name, Country, City, Address, Lat, Lng, IsActive, UpdatedAt";
        private static ParcelLocker Map(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            Provider = r.GetString(1),
            Code = r.GetString(2),
            Name = r.GetString(3),
            Country = r.GetString(4),
            City = r.GetStringOrNull(5),
            Address = r.GetStringOrNull(6),
            Lat = r.GetDecimalOrNull(7),
            Lng = r.GetDecimalOrNull(8),
            IsActive = r.GetBoolean(9),
            UpdatedAt = r.GetDateTime(10)
        };
        public async Task<ParcelLocker?> GetByIdAsync(int id) => await QuerySingleAsync($"SELECT {Columns} FROM ParcelLockers WHERE Id = @Id", Map, p => p.AddWithValue("@Id", id));
        public async Task<List<ParcelLocker>> SearchAsync(string country, string? query, int limit = 50)
        {
            var hasQuery = !string.IsNullOrWhiteSpace(query);
            var queryClause = hasQuery ? "AND (Name LIKE @Q ESCAPE '\\' OR City LIKE @Q ESCAPE '\\' OR Address LIKE @Q ESCAPE '\\')" : "";

            return await QueryAsync($"SELECT TOP (@Limit) {Columns} FROM ParcelLockers WHERE IsActive = 1 AND Country = @Country {queryClause} ORDER BY City, Name",
                Map, p =>
                {
                    p.AddWithValue("@Limit", Math.Clamp(limit, 1, 200));
                    p.AddWithValue("@Country", country);
                    if (hasQuery) p.AddWithValue("@Q", "%" + EscapeLike(query!.Trim()) + "%");
                });
        }
        public async Task<int> UpsertAsync(string provider, IReadOnlyCollection<ParcelLocker> lockers)
        {
            if (lockers.Count == 0) return 0;
            var now = DateTime.UtcNow;

            await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                foreach (var l in lockers)
                {
                    await using var cmd = new SqlCommand(@"MERGE ParcelLockers AS t
                        USING (SELECT @Provider AS Provider, @Code AS Code) AS s ON t.Provider = s.Provider AND t.Code = s.Code
                        WHEN MATCHED THEN UPDATE SET Name = @Name, Country = @Country, City = @City, Address = @Address, Lat = @Lat, Lng = @Lng, IsActive = 1, UpdatedAt = @Now
                        WHEN NOT MATCHED THEN INSERT (Provider, Code, Name, Country, City, Address, Lat, Lng, IsActive, UpdatedAt)
                                              VALUES (@Provider, @Code, @Name, @Country, @City, @Address, @Lat, @Lng, 1, @Now);", conn, tx);
                    cmd.Parameters.AddWithValue("@Provider", provider);
                    cmd.Parameters.AddWithValue("@Code", l.Code);
                    cmd.Parameters.AddWithValue("@Name", l.Name);
                    cmd.Parameters.AddWithValue("@Country", l.Country);
                    cmd.Parameters.AddWithValue("@City", (object?)l.City ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", (object?)l.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Lat", (object?)l.Lat ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Lng", (object?)l.Lng ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Now", now);
                    await cmd.ExecuteNonQueryAsync();
                }

                await using var deactivate = new SqlCommand("UPDATE ParcelLockers SET IsActive = 0 WHERE Provider = @Provider AND IsActive = 1 AND UpdatedAt < @Now", conn, tx);
                deactivate.Parameters.AddWithValue("@Provider", provider);
                deactivate.Parameters.AddWithValue("@Now", now);
                await deactivate.ExecuteNonQueryAsync();
            });

            return lockers.Count;
        }
        private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
    }
}