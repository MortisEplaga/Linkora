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

        // ── Existing methods ──

        public async Task<User?> GetByUsernameAsync(string username)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, PhoneNumber, Role, PasswordHash, AvatarImagePath FROM Users WHERE UserName = @U", conn);
            cmd.Parameters.AddWithValue("@U", username);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, PhoneNumber, Role, PasswordHash, AvatarImagePath FROM Users WHERE Id = @Id", conn);
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

        // ── Google OAuth additions ──

        /// <summary>Look up a user by email address (for Google sign-in).</summary>
        public async Task<User?> GetByEmailAsync(string email)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, PhoneNumber, Role, PasswordHash, AvatarImagePath FROM Users WHERE Email = @E", conn);
            cmd.Parameters.AddWithValue("@E", email);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }

        /// <summary>Create a Google-only account (no password hash).</summary>
        public async Task<int> CreateGoogleUserAsync(User user)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Users (UserName, Email, Role, AvatarImagePath, PasswordHash)
                OUTPUT INSERTED.Id
                VALUES (@U, @E, 'user', @A, NULL)", conn);
            cmd.Parameters.AddWithValue("@U", user.UserName);
            cmd.Parameters.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@A", (object?)user.AvatarImagePath ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        /// <summary>Update the avatar path (e.g. after Google login refreshes the picture URL).</summary>
        public async Task UpdateAvatarAsync(int userId, string avatarPath)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET AvatarImagePath = @A WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@A", avatarPath);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Return <paramref name="baseUsername"/> if it's free, otherwise append _2, _3, … until unique.
        /// </summary>
        public async Task<string> EnsureUniqueUsernameAsync(string baseUsername)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var candidate = baseUsername;
            var suffix = 2;

            while (true)
            {
                await using var cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM Users WHERE UserName = @U", conn);
                cmd.Parameters.AddWithValue("@U", candidate);
                var count = (int)(await cmd.ExecuteScalarAsync())!;
                if (count == 0) return candidate;
                candidate = $"{baseUsername}_{suffix++}";
            }
        }
    }
}