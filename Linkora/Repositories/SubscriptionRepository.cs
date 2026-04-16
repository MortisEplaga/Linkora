using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly string _connectionString;

        public SubscriptionRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        public async Task<List<SellerViewModel>> GetFollowingAsync(int followerId)
        {
            var result = new List<SellerViewModel>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT u.Id, u.UserName, u.AvatarImagePath, u.IsCompany, u.CreatedAt
                FROM Subscriptions s
                JOIN Users u ON u.Id = s.FollowingId
                WHERE s.FollowerId = @FollowerId
                ORDER BY u.UserName", conn);
            cmd.Parameters.AddWithValue("@FollowerId", followerId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result.Add(new SellerViewModel
                {
                    Id = r.GetInt32(0),
                    UserName = r.IsDBNull(1) ? null : r.GetString(1),
                    AvatarPath = r.IsDBNull(2) ? null : r.GetString(2),
                    IsCompany = !r.IsDBNull(3) && r.GetBoolean(3),
                    CreatedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
                });
            return result;
        }

        public async Task<bool> IsSubscribedAsync(int followerId, int followingId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Subscriptions WHERE FollowerId = @FollowerId AND FollowingId = @FollowingId", conn);
            cmd.Parameters.AddWithValue("@FollowerId", followerId);
            cmd.Parameters.AddWithValue("@FollowingId", followingId);
            var result = await cmd.ExecuteScalarAsync();
            return (result != null) && (int)result > 0;
        }

        public async Task<bool> ToggleAsync(int followerId, int followingId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

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
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Subscriptions WHERE FollowingId = @FollowingId", conn);
            cmd.Parameters.AddWithValue("@FollowingId", followingId);
            var result = await cmd.ExecuteScalarAsync();
            return (result != null) ? (int)result : 0;
        }
    }
}