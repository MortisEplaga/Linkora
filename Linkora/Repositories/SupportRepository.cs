using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class SupportRepository : ISupportRepository
    {
        private readonly string _connectionString;

        public SupportRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<int> CreateRequestAsync(string name, string email, string? phone, string message, string? userId)
        {
            const string sql = @"
                INSERT INTO SupportRequests (Name, Email, Phone, Message, CreatedAt, Status, UserId)
                VALUES (@Name, @Email, @Phone, @Message, @CreatedAt, @Status, @UserId);
                SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Name", name);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@Phone", (object?)phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Status", "New");
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

            var newId = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(newId);
        }
    }
}