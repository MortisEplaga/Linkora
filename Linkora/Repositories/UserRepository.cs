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
        private static bool HasColumn(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        private static User MapRow(SqlDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            UserName = r.GetString(r.GetOrdinal("UserName")),
            Email = r.IsDBNull(r.GetOrdinal("Email")) ? null : r.GetString(r.GetOrdinal("Email")),
            Phone = r.IsDBNull(r.GetOrdinal("Phone")) ? null : r.GetString(r.GetOrdinal("Phone")),
            Role = r.IsDBNull(r.GetOrdinal("Role")) ? null : r.GetString(r.GetOrdinal("Role")),
            PasswordHash = r.IsDBNull(r.GetOrdinal("PasswordHash")) ? null : r.GetString(r.GetOrdinal("PasswordHash")),
            AvatarUrl = r.IsDBNull(r.GetOrdinal("AvatarUrl")) ? null : r.GetString(r.GetOrdinal("AvatarUrl")),
            EmailConfirmed = !r.IsDBNull(r.GetOrdinal("EmailConfirmed")) && r.GetBoolean(r.GetOrdinal("EmailConfirmed")),
            PreferredAdDuration = r.IsDBNull(r.GetOrdinal("PreferredAdDuration")) ? null : r.GetInt32(r.GetOrdinal("PreferredAdDuration")),
            SubscriptionType = HasColumn(r, "SubscriptionType") && !r.IsDBNull(r.GetOrdinal("SubscriptionType"))
        ? r.GetString(r.GetOrdinal("SubscriptionType"))
        : "Free"
        };
        public async Task<User?> GetByPhoneAsync(string phone)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE Phone = @P", conn);
            cmd.Parameters.AddWithValue("@P", phone);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task<User?> GetByUsernameAsync(string username)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE UserName = @U", conn);
            cmd.Parameters.AddWithValue("@U", username);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task<User?> GetByIdAsync(int id)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration, SubscriptionType FROM Users WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task<int> CreateAsync(User user, string passwordHash)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
        INSERT INTO Users (UserName, Email, Phone, Role, PasswordHash, IsCompany, ConfirmationToken, EmailConfirmed)
        OUTPUT INSERTED.Id
        VALUES (@U, @E, @P, 'user', @H, @IC, @Token, 0)", conn);
            cmd.Parameters.AddWithValue("@U", user.UserName);
            cmd.Parameters.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@P", (object?)user.Phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@H", passwordHash);
            cmd.Parameters.AddWithValue("@IC", user.IsCompany);
            cmd.Parameters.AddWithValue("@Token", (object?)user.ConfirmationToken ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        public async Task<User?> GetByEmailAsync(string email)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE Email = @E", conn);
            cmd.Parameters.AddWithValue("@E", email);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task<int> CreateGoogleUserAsync(User user)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Users (UserName, Email, Role, AvatarUrl, PasswordHash)
                OUTPUT INSERTED.Id
                VALUES (@U, @E, 'user', @A, NULL)", conn);
            cmd.Parameters.AddWithValue("@U", user.UserName);
            cmd.Parameters.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@A", (object?)user.AvatarUrl ?? DBNull.Value);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        public async Task UpdateAvatarAsync(int userId, string avatarUrl)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET AvatarUrl = @A WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@A", avatarUrl);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
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
        public async Task<User?> GetByConfirmationTokenAsync(string token)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE ConfirmationToken = @T", conn);
            cmd.Parameters.AddWithValue("@T", token);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task ConfirmEmailAsync(string token)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET EmailConfirmed = 1, ConfirmationToken = NULL WHERE ConfirmationToken = @T", conn);
            cmd.Parameters.AddWithValue("@T", token);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<int> CreateExternalUserAsync(User user)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO Users (UserName, Email, Role, PasswordHash, AvatarUrl, EmailConfirmed, IsCompany, ConfirmationToken)
                OUTPUT INSERTED.Id
                VALUES (@U, @E, 'user', NULL, @A, @EC, @IC, NULL)", conn);
            cmd.Parameters.AddWithValue("@U", user.UserName);
            cmd.Parameters.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@A", (object?)user.AvatarUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EC", user.EmailConfirmed);
            cmd.Parameters.AddWithValue("@IC", user.IsCompany);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
        public async Task<User?> GetByFacebookIdAsync(string facebookId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, IsCompany, FacebookId, PreferredAdDuration FROM Users WHERE FacebookId = @FbId", conn);
            cmd.Parameters.AddWithValue("@FbId", facebookId);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task UpdateFacebookIdAsync(int userId, string facebookId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET FacebookId = @FbId WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@FbId", facebookId);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task MarkForDeletionAsync(int userId, string deletionRequestCode)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET DeletionRequestCode = @Code, DeletionRequestedAt = GETUTCDATE() WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Code", deletionRequestCode);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<User?> GetByDeletionCodeAsync(string code)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, IsCompany, FacebookId, PreferredAdDuration FROM Users WHERE DeletionRequestCode = @Code", conn);
            cmd.Parameters.AddWithValue("@Code", code);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task SetPasswordResetTokenAsync(int userId, string token, DateTime expiry)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET PasswordResetToken = @T, PasswordResetExpiry = @E WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@T", token);
            cmd.Parameters.AddWithValue("@E", expiry);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<User?> GetByPasswordResetTokenAsync(string token)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE PasswordResetToken = @T AND PasswordResetExpiry > GETUTCDATE()", conn);
            cmd.Parameters.AddWithValue("@T", token);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? MapRow(r) : null;
        }
        public async Task ClearPasswordResetTokenAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET PasswordResetToken = NULL, PasswordResetExpiry = NULL WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task UpdatePasswordHashAsync(int userId, string passwordHash)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET PasswordHash = @H WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@H", passwordHash);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task AdjustPromotionPointsAsync(int userId, int delta)
        {
            if (delta == 0) return;
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE Users SET PromotionPoints = PromotionPoints + @Delta WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Delta", delta);
            cmd.Parameters.AddWithValue("@Id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task<bool> IsBannedAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            return user?.Role == "banned";
        }
    }
}