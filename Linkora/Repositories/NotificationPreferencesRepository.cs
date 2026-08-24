using Linkora.Models;

namespace Linkora.Repositories
{
    public class NotificationPreferencesRepository : SqlRepositoryBase, INotificationPreferencesRepository
    {
        public NotificationPreferencesRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<NotificationPreferences> GetAsync(int userId) => (await QuerySingleAsync(
                "SELECT Deals, Reviews, Moderation, Account, Favourites, NewListings FROM NotificationPreferences WHERE UserId = @UserId",
                r => new NotificationPreferences
                {
                    UserId = userId,
                    Deals = r.GetBoolean(0),
                    Reviews = r.GetBoolean(1),
                    Moderation = r.GetBoolean(2),
                    Account = r.GetBoolean(3),
                    Favourites = r.GetBoolean(4),
                    NewListings = r.GetBoolean(5),
                },
                p => p.AddWithValue("@UserId", userId))) ?? new NotificationPreferences { UserId = userId };
        public async Task SaveAsync(NotificationPreferences prefs) => await ExecuteAsync(@"
                MERGE NotificationPreferences AS target
                USING (SELECT @UserId AS UserId) AS source
                ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET Deals = @Deals, Reviews = @Reviews, Moderation = @Moderation,
                               Account = @Account, Favourites = @Favourites, NewListings = @NewListings
                WHEN NOT MATCHED THEN
                    INSERT (UserId, Deals, Reviews, Moderation, Account, Favourites, NewListings)
                    VALUES (@UserId, @Deals, @Reviews, @Moderation, @Account, @Favourites, @NewListings);",
                p =>
                {
                    p.AddWithValue("@UserId", prefs.UserId);
                    p.AddWithValue("@Deals", prefs.Deals);
                    p.AddWithValue("@Reviews", prefs.Reviews);
                    p.AddWithValue("@Moderation", prefs.Moderation);
                    p.AddWithValue("@Account", prefs.Account);
                    p.AddWithValue("@Favourites", prefs.Favourites);
                    p.AddWithValue("@NewListings", prefs.NewListings);
                });
    }
}