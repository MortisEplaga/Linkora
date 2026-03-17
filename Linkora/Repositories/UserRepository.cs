using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;

        public UserRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        private static User MapRow(SqlDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            UserName = r.GetString(r.GetOrdinal("UserName")),
            Email = r.IsDBNull(r.GetOrdinal("Email")) ? null : r.GetString(r.GetOrdinal("Email")),
            PhoneNumber = r.IsDBNull(r.GetOrdinal("PhoneNumber")) ? null : r.GetString(r.GetOrdinal("PhoneNumber")),
            Role = r.IsDBNull(r.GetOrdinal("Role")) ? null : r.GetString(r.GetOrdinal("Role")),
            PasswordHash = r.IsDBNull(r.GetOrdinal("PasswordHash")) ? null : r.GetString(r.GetOrdinal("PasswordHash")),
            AvatarImagePath = r.IsDBNull(r.GetOrdinal("AvatarImagePath")) ? null : r.GetString(r.GetOrdinal("AvatarImagePath")),
        };

        public async Task<User?> GetByUsernameAsync(string username)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, PhoneNumber, Role, PasswordHash, AvatarImagePath FROM Users WHERE UserName = @U", conn);
            cmd.Parameters.AddWithValue("@U", username); // ← эта строка должна быть
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, PhoneNumber, Role, PasswordHash FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }

        public async Task<int> CreateAsync(User user, string passwordHash)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Users (UserName, Email, PhoneNumber, Role, PasswordHash)
                OUTPUT INSERTED.Id
                VALUES (@U, @E, @P, 'user', @H)", conn);
            cmd.Parameters.AddWithValue("@U", user.UserName);
            cmd.Parameters.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@P", (object?)user.PhoneNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@H", passwordHash);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
    }
}