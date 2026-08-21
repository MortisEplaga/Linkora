using Linkora.Models;

namespace Linkora.Repositories
{
    public class SubscriptionRepository : SqlRepositoryBase, ISubscriptionRepository
    {
        public SubscriptionRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<List<UserSummary>> GetFollowingAsync(int followerId)
        {
            return await QueryAsync(
                @"SELECT u.Id, u.UserName, u.AvatarUrl, u.IsCompany, u.CreatedAt
                  FROM Subscriptions s
                  JOIN Users u ON u.Id = s.FollowingId
                  WHERE s.FollowerId = @FollowerId
                  ORDER BY u.UserName",
                r => new UserSummary
                {
                    Id = r.GetInt32(0),
                    UserName = r.IsDBNull(1) ? null : r.GetString(1),
                    AvatarUrl = r.IsDBNull(2) ? null : r.GetString(2),
                    IsCompany = !r.IsDBNull(3) && r.GetBoolean(3),
                    CreatedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
                },
                p => p.AddWithValue("@FollowerId", followerId));
        }
        public async Task<bool> IsSubscribedAsync(int followerId, int followingId)
        {
            var result = await QueryAsync<int>(
                "SELECT COUNT(1) FROM Subscriptions WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@FollowerId", followerId);
                    p.AddWithValue("@FollowingId", followingId);
                });
            return result.Count > 0 && result[0] > 0;
        }
        public async Task<bool> ToggleAsync(int followerId, int followingId)
        {
            var existing = (await QueryAsync<int?>(
                "SELECT Id FROM Subscriptions WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@FollowerId", followerId);
                    p.AddWithValue("@FollowingId", followingId);
                })).FirstOrDefault();

            if (existing != null)
            {
                await ExecuteAsync("DELETE FROM Subscriptions WHERE Id = @Id",
                    p => p.AddWithValue("@Id", existing.Value));
                return false;
            }
            else
            {
                await ExecuteAsync("INSERT INTO Subscriptions (FollowerId, FollowingId) VALUES (@FollowerId, @FollowingId)",
                    p =>
                    {
                        p.AddWithValue("@FollowerId", followerId);
                        p.AddWithValue("@FollowingId", followingId);
                    });
                return true;
            }
        }
        public async Task<int> GetSubscriberCountAsync(int followingId)
        {
            var result = await QueryAsync<int>(
                "SELECT COUNT(1) FROM Subscriptions WHERE FollowingId = @FollowingId",
                r => r.GetInt32(0),
                p => p.AddWithValue("@FollowingId", followingId));
            return result.Count > 0 ? result[0] : 0;
        }
    }
}