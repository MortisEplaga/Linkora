using Linkora.Models;

namespace Linkora.Repositories
{
    public class NotificationPreferencesRepository : SqlRepositoryBase, INotificationPreferencesRepository
    {
        public NotificationPreferencesRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<NotificationPreferences> GetAsync(int userId) => (await QuerySingleAsync(
                "SELECT Deals, Reviews, Moderation, Account, Favourites, NewListings, ExpiringSoon, EmailDeals, EmailReviews, EmailModeration, EmailAccount, EmailFavourites, EmailNewListings, EmailExpiringSoon FROM NotificationPreferences WHERE UserId = @UserId",
                r => new NotificationPreferences
                {
                    UserId = userId,
                    Deals = r.GetBoolean(0),
                    Reviews = r.GetBoolean(1),
                    Moderation = r.GetBoolean(2),
                    Account = r.GetBoolean(3),
                    Favourites = r.GetBoolean(4),
                    NewListings = r.GetBoolean(5),
                    ExpiringSoon = r.GetBoolean(6),
                    EmailDeals = r.GetBoolean(7),
                    EmailReviews = r.GetBoolean(8),
                    EmailModeration = r.GetBoolean(9),
                    EmailAccount = r.GetBoolean(10),
                    EmailFavourites = r.GetBoolean(11),
                    EmailNewListings = r.GetBoolean(12),
                    EmailExpiringSoon = r.GetBoolean(13),
                },
                p => p.AddWithValue("@UserId", userId))) ?? new NotificationPreferences { UserId = userId };
        public async Task SaveAsync(NotificationPreferences prefs) => await ExecuteAsync(@"
            MERGE NotificationPreferences AS target
            USING (SELECT @UserId AS UserId) AS source
            ON target.UserId = source.UserId
            WHEN MATCHED THEN
                UPDATE SET Deals = @Deals, Reviews = @Reviews, Moderation = @Moderation,
                           Account = @Account, Favourites = @Favourites, NewListings = @NewListings, ExpiringSoon = @ExpiringSoon,
                           EmailDeals = @EmailDeals, EmailReviews = @EmailReviews, EmailModeration = @EmailModeration,
                           EmailAccount = @EmailAccount, EmailFavourites = @EmailFavourites, EmailNewListings = @EmailNewListings,
                           EmailExpiringSoon = @EmailExpiringSoon
            WHEN NOT MATCHED THEN
                INSERT (UserId, Deals, Reviews, Moderation, Account, Favourites, NewListings, ExpiringSoon,
                        EmailDeals, EmailReviews, EmailModeration, EmailAccount, EmailFavourites, EmailNewListings, EmailExpiringSoon)
                VALUES (@UserId, @Deals, @Reviews, @Moderation, @Account, @Favourites, @NewListings, @ExpiringSoon,
                        @EmailDeals, @EmailReviews, @EmailModeration, @EmailAccount, @EmailFavourites, @EmailNewListings, @EmailExpiringSoon);",
        p =>
        {
            p.AddWithValue("@UserId", prefs.UserId);
            p.AddWithValue("@Deals", prefs.Deals);
            p.AddWithValue("@Reviews", prefs.Reviews);
            p.AddWithValue("@Moderation", prefs.Moderation);
            p.AddWithValue("@Account", prefs.Account);
            p.AddWithValue("@Favourites", prefs.Favourites);
            p.AddWithValue("@NewListings", prefs.NewListings);
            p.AddWithValue("@ExpiringSoon", prefs.ExpiringSoon);
            p.AddWithValue("@EmailDeals", prefs.EmailDeals);
            p.AddWithValue("@EmailReviews", prefs.EmailReviews);
            p.AddWithValue("@EmailModeration", prefs.EmailModeration);
            p.AddWithValue("@EmailAccount", prefs.EmailAccount);
            p.AddWithValue("@EmailFavourites", prefs.EmailFavourites);
            p.AddWithValue("@EmailNewListings", prefs.EmailNewListings);
            p.AddWithValue("@EmailExpiringSoon", prefs.EmailExpiringSoon);
        });
    }
}