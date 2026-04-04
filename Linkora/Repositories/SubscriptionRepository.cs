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

        public async Task<bool> IsSubscribedAsync(int followerId, int sellerId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Subscriptions WHERE FollowerId = @F AND SellerId = @S", conn);
            cmd.Parameters.AddWithValue("@F", followerId);
            cmd.Parameters.AddWithValue("@S", sellerId);
            return (int)(await cmd.ExecuteScalarAsync())! > 0;
        }

        public async Task<bool> ToggleAsync(int followerId, int sellerId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var checkCmd = new SqlCommand(
                "SELECT Id FROM Subscriptions WHERE FollowerId = @F AND SellerId = @S", conn);
            checkCmd.Parameters.AddWithValue("@F", followerId);
            checkCmd.Parameters.AddWithValue("@S", sellerId);
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
                    "INSERT INTO Subscriptions (FollowerId, SellerId) VALUES (@F, @S)", conn);
                insCmd.Parameters.AddWithValue("@F", followerId);
                insCmd.Parameters.AddWithValue("@S", sellerId);
                await insCmd.ExecuteNonQueryAsync();
                return true; 
            }
        }

        public async Task<int> GetSubscriberCountAsync(int sellerId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Subscriptions WHERE SellerId = @S", conn);
            cmd.Parameters.AddWithValue("@S", sellerId);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
    }
}