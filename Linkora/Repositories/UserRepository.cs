using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class UserRepository : SqlRepositoryBase, IUserRepository
    {
        public UserRepository(IConfiguration configuration) : base(configuration) { }
        private static bool HasColumn(SqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
        private static User MapUser(SqlDataReader r) => new()
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
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE Phone = @P",
                MapUser,
                p => p.AddWithValue("@P", phone));
        }
        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE UserName = @U",
                MapUser,
                p => p.AddWithValue("@U", username));
        }
        public async Task<User?> GetByIdAsync(int id)
        {
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration, SubscriptionType FROM Users WHERE Id = @Id",
                MapUser,
                p => p.AddWithValue("@Id", id));
        }
        public async Task<int> CreateAsync(User user, string passwordHash)
        {
            var result = await QueryAsync<int>(
                @"INSERT INTO Users (UserName, Email, Phone, Role, PasswordHash, IsCompany, ConfirmationToken, EmailConfirmed)
                  OUTPUT INSERTED.Id
                  VALUES (@U, @E, @P, 'user', @H, @IC, @Token, 0)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@U", user.UserName);
                    p.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
                    p.AddWithValue("@P", (object?)user.Phone ?? DBNull.Value);
                    p.AddWithValue("@H", passwordHash);
                    p.AddWithValue("@IC", user.IsCompany);
                    p.AddWithValue("@Token", (object?)user.ConfirmationToken ?? DBNull.Value);
                });
            return result[0];
        }
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE Email = @E",
                MapUser,
                p => p.AddWithValue("@E", email));
        }
        public async Task<int> CreateGoogleUserAsync(User user)
        {
            var result = await QueryAsync<int>(
                @"INSERT INTO Users (UserName, Email, Role, AvatarUrl, PasswordHash)
                  OUTPUT INSERTED.Id
                  VALUES (@U, @E, 'user', @A, NULL)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@U", user.UserName);
                    p.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
                    p.AddWithValue("@A", (object?)user.AvatarUrl ?? DBNull.Value);
                });
            return result[0];
        }
        public async Task UpdateAvatarAsync(int userId, string avatarUrl)
        {
            await ExecuteAsync(
                "UPDATE Users SET AvatarUrl = @A WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@A", avatarUrl);
                    p.AddWithValue("@Id", userId);
                });
        }
        public async Task<string> EnsureUniqueUsernameAsync(string baseUsername)
        {
            await using var conn = await OpenConnectionAsync();

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
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE ConfirmationToken = @T",
                MapUser,
                p => p.AddWithValue("@T", token));
        }
        public async Task ConfirmEmailAsync(string token)
        {
            await ExecuteAsync(
                "UPDATE Users SET EmailConfirmed = 1, ConfirmationToken = NULL WHERE ConfirmationToken = @T",
                p => p.AddWithValue("@T", token));
        }
        public async Task<int> CreateExternalUserAsync(User user)
        {
            var result = await QueryAsync<int>(
                @"INSERT INTO Users (UserName, Email, Role, PasswordHash, AvatarUrl, EmailConfirmed, IsCompany, ConfirmationToken)
                  OUTPUT INSERTED.Id
                  VALUES (@U, @E, 'user', NULL, @A, @EC, @IC, NULL)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@U", user.UserName);
                    p.AddWithValue("@E", (object?)user.Email ?? DBNull.Value);
                    p.AddWithValue("@A", (object?)user.AvatarUrl ?? DBNull.Value);
                    p.AddWithValue("@EC", user.EmailConfirmed);
                    p.AddWithValue("@IC", user.IsCompany);
                });
            return result[0];
        }
        public async Task<User?> GetByFacebookIdAsync(string facebookId)
        {
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, IsCompany, FacebookId, PreferredAdDuration FROM Users WHERE FacebookId = @FbId",
                MapUser,
                p => p.AddWithValue("@FbId", facebookId));
        }
        public async Task UpdateFacebookIdAsync(int userId, string facebookId)
        {
            await ExecuteAsync(
                "UPDATE Users SET FacebookId = @FbId WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@FbId", facebookId);
                    p.AddWithValue("@Id", userId);
                });
        }
        public async Task MarkForDeletionAsync(int userId, string deletionRequestCode)
        {
            await ExecuteAsync(
                "UPDATE Users SET DeletionRequestCode = @Code, DeletionRequestedAt = GETUTCDATE() WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Code", deletionRequestCode);
                    p.AddWithValue("@Id", userId);
                });
        }
        public async Task<User?> GetByDeletionCodeAsync(string code)
        {
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, IsCompany, FacebookId, PreferredAdDuration FROM Users WHERE DeletionRequestCode = @Code",
                MapUser,
                p => p.AddWithValue("@Code", code));
        }
        public async Task SetPasswordResetTokenAsync(int userId, string token, DateTime expiry)
        {
            await ExecuteAsync(
                "UPDATE Users SET PasswordResetToken = @T, PasswordResetExpiry = @E WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@T", token);
                    p.AddWithValue("@E", expiry);
                    p.AddWithValue("@Id", userId);
                });
        }
        public async Task<User?> GetByPasswordResetTokenAsync(string token)
        {
            return await QuerySingleAsync(
                "SELECT Id, UserName, Email, Phone, Role, PasswordHash, AvatarUrl, EmailConfirmed, PreferredAdDuration FROM Users WHERE PasswordResetToken = @T AND PasswordResetExpiry > GETUTCDATE()",
                MapUser,
                p => p.AddWithValue("@T", token));
        }
        public async Task ClearPasswordResetTokenAsync(int userId)
        {
            await ExecuteAsync(
                "UPDATE Users SET PasswordResetToken = NULL, PasswordResetExpiry = NULL WHERE Id = @Id",
                p => p.AddWithValue("@Id", userId));
        }
        public async Task UpdatePasswordHashAsync(int userId, string passwordHash)
        {
            await ExecuteAsync(
                "UPDATE Users SET PasswordHash = @H WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@H", passwordHash);
                    p.AddWithValue("@Id", userId);
                });
        }
        public async Task AdjustPromotionPointsAsync(int userId, int delta)
        {
            if (delta == 0) return;
            await ExecuteAsync(
                "UPDATE Users SET PromotionPoints = PromotionPoints + @Delta WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Delta", delta);
                    p.AddWithValue("@Id", userId);
                });
        }
        public async Task<bool> IsBannedAsync(int userId)
        {
            var user = await GetByIdAsync(userId);
            return user?.Role == "banned";
        }
    }
}