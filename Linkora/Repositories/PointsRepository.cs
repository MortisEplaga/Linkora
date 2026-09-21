using Linkora.Models;
using Linkora.Services;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class PointsRepository(IConfiguration configuration) : SqlRepositoryBase(configuration), IPointsRepository
    {
        public async Task<int> GetBalanceAsync(int userId)
        {
            await using var conn = await OpenConnectionAsync();
            return PointsBalanceCalculator.Calculate(await LoadEventsAsync(conn, null, userId), DateTime.UtcNow);
        }
        public Task<bool> SpendOnSubscriptionAsync(int userId, PromotionTier tier, PromotionTermType term, decimal eurPrice, int pointsCost, DateTime startedAt, DateTime expiresAt, int? supersedeId) => SpendAsync(userId, pointsCost, async (conn, tx) =>
        {
            if (supersedeId.HasValue)
            {
                await using var sup = new SqlCommand("UPDATE Promotion SET Status = @S WHERE Id = @Id AND UserId = @U", conn, tx);
                sup.Parameters.AddWithValue("@S", (short)PromotionStatus.Superseded);
                sup.Parameters.AddWithValue("@Id", supersedeId.Value);
                sup.Parameters.AddWithValue("@U", userId);
                await sup.ExecuteNonQueryAsync();
            }

            await using var ins = new SqlCommand(@"INSERT INTO Promotion (UserId, Tier, TermType, StartedAt, ExpiresAt, PricePaid, Status, PaymentId) VALUES (@U, @Tier, @Term, @Start, @Exp, @Price, @Active, NULL)", conn, tx);
            ins.Parameters.AddWithValue("@U", userId);
            ins.Parameters.AddWithValue("@Tier", (short)tier);
            ins.Parameters.AddWithValue("@Term", (short)term);
            ins.Parameters.AddWithValue("@Start", startedAt);
            ins.Parameters.AddWithValue("@Exp", expiresAt);
            ins.Parameters.AddWithValue("@Price", eurPrice);
            ins.Parameters.AddWithValue("@Active", (short)PromotionStatus.Active);
            await ins.ExecuteNonQueryAsync();
                                                   });
        public Task<bool> SpendOnListingBoostAsync(int userId, int productId, PromotionTier tier, DateTime expiresAt, int pointsCost) => SpendAsync(userId, pointsCost, async (conn, tx) =>
        {
            await using var cmd = new SqlCommand(@"UPDATE Products SET PaidBoostLevel = @Tier, PaidBoostExpiresAt = @Exp, PaidBoostPoints = @Pts WHERE Id = @Id AND UserId = @U", conn, tx);
            cmd.Parameters.AddWithValue("@Tier", (short)tier);
            cmd.Parameters.AddWithValue("@Exp", expiresAt);
            cmd.Parameters.AddWithValue("@Pts", pointsCost);
            cmd.Parameters.AddWithValue("@Id", productId);
            cmd.Parameters.AddWithValue("@U", userId);
            if (await cmd.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Product not found or not owned by user");
        });
        private Task<bool> SpendAsync(int userId, int pointsCost, Func<SqlConnection, SqlTransaction, Task> apply) => ExecuteInTransactionAsync(async (conn, tx) =>
        {
            await using (var lockCmd = new SqlCommand("SELECT Id FROM Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id", conn, tx))
            {
                lockCmd.Parameters.AddWithValue("@Id", userId);
                if (await lockCmd.ExecuteScalarAsync() == null) return false;
            }

            var balance = PointsBalanceCalculator.Calculate(await LoadEventsAsync(conn, tx, userId), DateTime.UtcNow);
            if (pointsCost <= 0 || balance < pointsCost) return false;

            await apply(conn, tx);
            return true;
        });
        private static async Task<List<PointsEvent>> LoadEventsAsync(SqlConnection conn, SqlTransaction? tx, int userId)
        {
            const string sql = @"SELECT CompletedAt, CAST(ROUND(Price * 100, 0) AS int), 1
                FROM Payments WHERE UserId = @U AND Status = 'Completed' AND CompletedAt IS NOT NULL
                UNION ALL
                SELECT StartedAt, CAST(ROUND(PricePaid * 100, 0) AS int) * 10, 0
                FROM Promotion WHERE UserId = @U AND PaymentId IS NULL
                UNION ALL
                SELECT MIN(HisAt), MAX(PaidBoostPoints), 0
                FROM HIS_Products WHERE UserId = @U AND PaidBoostPoints > 0
                GROUP BY ProductId, PaidBoostExpiresAt";

            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@U", userId);
            var result = new List<PointsEvent>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) result.Add(new PointsEvent(r.GetDateTime(0), r.GetInt32(1), r.GetInt32(2) == 1));
            return result;
        }
    }
}