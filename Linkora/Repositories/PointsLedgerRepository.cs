using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class PointsLedgerRepository : SqlRepositoryBase, IPointsLedgerRepository
    {
        public PointsLedgerRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<int?> TryAddAsync(int userId, PointsLedgerEventType eventType, int? sourceUserId = null, int? sourceProductId = null)
        {
            var rule = PointsLedgerRules.Rules[eventType];
            var now = DateTime.UtcNow;
            var monthKey = now.ToString("yyyyMM");
            var typeName = eventType.ToString();

            return await ExecuteInTransactionAsync(async (conn, tx) =>
            {
                await using (var lockCmd = new SqlCommand("SELECT Id FROM Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id", conn, tx))
                {
                    lockCmd.Parameters.AddWithValue("@Id", userId);
                    if (await lockCmd.ExecuteScalarAsync() == null) return (int?)null;
                }

                if (rule.OneTime)
                {
                    await using var existsCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM PointsLedgerEntries WHERE UserId = @UserId AND EventType = @EventType AND Status != 'Rejected'", conn, tx);
                    existsCmd.Parameters.AddWithValue("@UserId", userId);
                    existsCmd.Parameters.AddWithValue("@EventType", typeName);
                    if ((int)(await existsCmd.ExecuteScalarAsync())! > 0) return (int?)null;
                }
                else
                {
                    await using var capCmd = new SqlCommand(
                        @"SELECT COUNT(*), ISNULL(SUM(Points),0) FROM PointsLedgerEntries
                          WHERE UserId = @UserId AND EventType = @EventType AND MonthKey = @MonthKey AND Status != 'Rejected'", conn, tx);
                    capCmd.Parameters.AddWithValue("@UserId", userId);
                    capCmd.Parameters.AddWithValue("@EventType", typeName);
                    capCmd.Parameters.AddWithValue("@MonthKey", monthKey);
                    await using var reader = await capCmd.ExecuteReaderAsync();
                    await reader.ReadAsync();
                    var count = reader.GetInt32(0);
                    var pointsSoFar = reader.GetInt32(1);
                    await reader.CloseAsync();

                    if (rule.MonthlyCountCap.HasValue && count >= rule.MonthlyCountCap.Value) return (int?)null;
                    if (rule.MonthlyPointsCap.HasValue && pointsSoFar + rule.Points > rule.MonthlyPointsCap.Value) return (int?)null;
                }

                var status = rule.HoldDays <= 0 ? "Available" : "Pending";
                var availableAt = now.AddDays(rule.HoldDays);

                await using (var insertCmd = new SqlCommand(
                    @"INSERT INTO PointsLedgerEntries (UserId, Points, EventType, Status, SourceUserId, SourceProductId, MonthKey, CreatedAt, AvailableAt, ConfirmedAt)
                      OUTPUT INSERTED.Id
                      VALUES (@UserId, @Points, @EventType, @Status, @SourceUserId, @SourceProductId, @MonthKey, @CreatedAt, @AvailableAt, @ConfirmedAt)", conn, tx))
                {
                    insertCmd.Parameters.AddWithValue("@UserId", userId);
                    insertCmd.Parameters.AddWithValue("@Points", rule.Points);
                    insertCmd.Parameters.AddWithValue("@EventType", typeName);
                    insertCmd.Parameters.AddWithValue("@Status", status);
                    insertCmd.Parameters.AddWithValue("@SourceUserId", (object?)sourceUserId ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@SourceProductId", (object?)sourceProductId ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@MonthKey", monthKey);
                    insertCmd.Parameters.AddWithValue("@CreatedAt", now);
                    insertCmd.Parameters.AddWithValue("@AvailableAt", availableAt);
                    insertCmd.Parameters.AddWithValue("@ConfirmedAt", (object?)(status == "Available" ? now : null) ?? DBNull.Value);

                    return (int?)(int)(await insertCmd.ExecuteScalarAsync())!;
                }
            });
        }
        public async Task RecordListingPostedAsync(int sellerId, int productId)
        {
            var listingEntryId = await TryAddAsync(sellerId, PointsLedgerEventType.ListingPosted, sourceProductId: productId);
            if (listingEntryId == null) return;

            var referrerId = (await QueryAsync<int?>(
                "SELECT ReferrerId FROM Users WHERE Id = @Id",
                r => r.GetInt32OrNull(0),
                p => p.AddWithValue("@Id", sellerId))).FirstOrDefault();

            if (!referrerId.HasValue) return;

            var listingCount = (await QueryAsync<int>(
                "SELECT COUNT(*) FROM PointsLedgerEntries WHERE UserId = @UserId AND EventType = 'ListingPosted' AND Status != 'Rejected'",
                r => r.GetInt32(0),
                p => p.AddWithValue("@UserId", sellerId))).FirstOrDefault();

            PointsLedgerEventType? referralEvent = listingCount switch
            {
                1 => PointsLedgerEventType.ReferralFirstListing,
                5 => PointsLedgerEventType.ReferralFiveListings,
                _ => null
            };

            if (referralEvent.HasValue)
                await TryAddAsync(referrerId.Value, referralEvent.Value, sourceUserId: sellerId, sourceProductId: productId);
        }
        public async Task<PointsSummary> GetSummaryAsync(int userId)
        {
            var result = await QueryAsync(
                @"SELECT
                    ISNULL(SUM(CASE WHEN Status = 'Available' THEN Points END), 0),
                    ISNULL(SUM(CASE WHEN Status = 'Pending' THEN Points END), 0)
                  FROM PointsLedgerEntries WHERE UserId = @UserId",
                r => new PointsSummary { Available = r.GetInt32(0), Pending = r.GetInt32(1) },
                p => p.AddWithValue("@UserId", userId));

            return result.FirstOrDefault() ?? new PointsSummary();
        }
        public async Task<(int Earned, int Pending)> GetReferralPointsAsync(int userId)
        {
            var result = await QueryAsync(
                @"SELECT
                    ISNULL(SUM(CASE WHEN Status = 'Available' THEN Points END), 0),
                    ISNULL(SUM(CASE WHEN Status = 'Pending' THEN Points END), 0)
                  FROM PointsLedgerEntries
                  WHERE UserId = @UserId AND Status <> 'Rejected'
                    AND EventType IN ('ReferralFirstListing', 'ReferralFiveListings')",
                r => (Earned: r.GetInt32(0), Pending: r.GetInt32(1)),
                p => p.AddWithValue("@UserId", userId));

            return result.FirstOrDefault();
        }
        public async Task<List<PointsLedgerEntry>> GetHistoryAsync(int userId, int limit = 50) => await QueryAsync(
                @"SELECT TOP (@Limit) Id, UserId, Points, EventType, Status, SourceUserId, SourceProductId, MonthKey, CreatedAt, AvailableAt, ConfirmedAt
                  FROM PointsLedgerEntries WHERE UserId = @UserId ORDER BY CreatedAt DESC",
                r => new PointsLedgerEntry
                {
                    Id = r.GetInt32(0),
                    UserId = r.GetInt32(1),
                    Points = r.GetInt32(2),
                    EventType = Enum.Parse<PointsLedgerEventType>(r.GetString(3)),
                    Status = Enum.Parse<PointsLedgerStatus>(r.GetString(4)),
                    SourceUserId = r.GetInt32OrNull(5),
                    SourceProductId = r.GetInt32OrNull(6),
                    MonthKey = r.GetStringOrDefault(7),
                    CreatedAt = r.GetDateTime(8),
                    AvailableAt = r.GetDateTime(9),
                    ConfirmedAt = r.GetDateTimeOrNull(10),
                },
                p =>
                {
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@Limit", limit);
                });
        public async Task<int> PromoteDueEntriesAsync()
        {
            var due = await QueryAsync(
                @"SELECT Id, UserId, EventType, SourceUserId, SourceProductId FROM PointsLedgerEntries
                  WHERE Status = 'Pending' AND AvailableAt <= SYSUTCDATETIME()",
                r => (Id: r.GetInt32(0), UserId: r.GetInt32(1), EventType: r.GetString(2), SourceUserId: r.GetInt32OrNull(3), SourceProductId: r.GetInt32OrNull(4)));

            if (due.Count == 0) return 0;

            var promoted = 0;

            foreach (var entry in due)
            {
                var eligible = await IsStillEligibleAsync(entry.UserId, entry.SourceUserId, entry.SourceProductId);
                var newStatus = eligible ? "Available" : "Rejected";

                await ExecuteAsync(
                    "UPDATE PointsLedgerEntries SET Status = @Status, ConfirmedAt = @Now WHERE Id = @Id",
                    p =>
                    {
                        p.AddWithValue("@Status", newStatus);
                        p.AddWithValue("@Now", DateTime.UtcNow);
                        p.AddWithValue("@Id", entry.Id);
                    });

                if (eligible) promoted++;
            }

            return promoted;
        }
        private async Task<bool> IsStillEligibleAsync(int userId, int? sourceUserId, int? sourceProductId)
        {
            var recipientRole = (await QueryAsync<string>(
                "SELECT Role FROM Users WHERE Id = @Id",
                r => r.GetStringOrNull(0)!,
                p => p.AddWithValue("@Id", userId))).FirstOrDefault();
            if (recipientRole == null || recipientRole == "banned") return false;

            var checkUserId = sourceUserId ?? userId;

            if (checkUserId != userId)
            {
                var sourceRole = (await QueryAsync<string>(
                    "SELECT Role FROM Users WHERE Id = @Id",
                    r => r.GetStringOrNull(0)!,
                    p => p.AddWithValue("@Id", checkUserId))).FirstOrDefault();
                if (sourceRole == null || sourceRole == "banned") return false;
            }

            if (sourceProductId.HasValue)
            {
                var status = (await QueryAsync<string>(
                    "SELECT Status FROM Products WHERE Id = @Id",
                    r => r.GetStringOrNull(0)!,
                    p => p.AddWithValue("@Id", sourceProductId.Value))).FirstOrDefault();
                if (status == null || status is "Rejected" or "Moderation") return false;
            }

            return true;
        }
    }
}