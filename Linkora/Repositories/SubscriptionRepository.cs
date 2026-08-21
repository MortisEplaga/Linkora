using Linkora.Models;
using Microsoft.Data.SqlClient;

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
            await using var conn = await OpenConnectionAsync();

            await using var checkCmd = new SqlCommand(
                "SELECT Id FROM Subscriptions WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId", conn);
            checkCmd.Parameters.AddWithValue("@FollowerId", followerId);
            checkCmd.Parameters.AddWithValue("@FollowingId", followingId);
            var existing = await checkCmd.ExecuteScalarAsync();

            if (existing != null)
            {
                await using var delCmd = new SqlCommand(
                    "DELETE FROM Subscriptions WHERE Id = @Id", conn);
                delCmd.Parameters.AddWithValue("@Id", existing);
                await delCmd.ExecuteNonQueryAsync();
                return false;
            }
            else
            {
                await using var insCmd = new SqlCommand(
                    "INSERT INTO Subscriptions (FollowerId, FollowingId) VALUES (@FollowerId, @FollowingId)", conn);
                insCmd.Parameters.AddWithValue("@FollowerId", followerId);
                insCmd.Parameters.AddWithValue("@FollowingId", followingId);
                await insCmd.ExecuteNonQueryAsync();
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