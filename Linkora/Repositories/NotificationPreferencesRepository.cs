using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class NotificationPreferencesRepository : INotificationPreferencesRepository
    {
        private readonly string _connectionString;

        public NotificationPreferencesRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<NotificationPreferences> GetAsync(int userId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT Deals, Reviews, Moderation, Account, Favourites, NewListings FROM NotificationPreferences WHERE UserId = @UserId", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new NotificationPreferences
                {
                    UserId = userId,
                    Deals = r.GetBoolean(0),
                    Reviews = r.GetBoolean(1),
                    Moderation = r.GetBoolean(2),
                    Account = r.GetBoolean(3),
                    Favourites = r.GetBoolean(4),
                    NewListings = r.GetBoolean(5),
                };
            }
            return new NotificationPreferences { UserId = userId };
        }

        public async Task SaveAsync(NotificationPreferences prefs)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                MERGE NotificationPreferences AS target
                USING (SELECT @UserId AS UserId) AS source
                ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET Deals = @Deals, Reviews = @Reviews, Moderation = @Moderation,
                               Account = @Account, Favourites = @Favourites, NewListings = @NewListings
                WHEN NOT MATCHED THEN
                    INSERT (UserId, Deals, Reviews, Moderation, Account, Favourites, NewListings)
                    VALUES (@UserId, @Deals, @Reviews, @Moderation, @Account, @Favourites, @NewListings);", conn);
            cmd.Parameters.AddWithValue("@UserId", prefs.UserId);
            cmd.Parameters.AddWithValue("@Deals", prefs.Deals);
            cmd.Parameters.AddWithValue("@Reviews", prefs.Reviews);
            cmd.Parameters.AddWithValue("@Moderation", prefs.Moderation);
            cmd.Parameters.AddWithValue("@Account", prefs.Account);
            cmd.Parameters.AddWithValue("@Favourites", prefs.Favourites);
            cmd.Parameters.AddWithValue("@NewListings", prefs.NewListings);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}