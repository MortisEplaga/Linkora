using Linkora.Models;

namespace Linkora.Repositories
{
    public class PromotionRepository : SqlRepositoryBase, IPromotionRepository
    {
        public PromotionRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<Promotion?> GetActiveAsync(int userId) => await QuerySingleAsync(
            @"SELECT TOP 1 Id, UserId, Tier, TermType, StartedAt, ExpiresAt, PricePaid, Status FROM Promotion
              WHERE UserId = @UserId AND Status = @Active AND ExpiresAt > SYSUTCDATETIME() ORDER BY ExpiresAt DESC",
            r => new Promotion
            {
                Id = r.GetInt32(0),
                UserId = r.GetInt32(1),
                Tier = (PromotionTier)r.GetInt16(2),
                TermType = (PromotionTermType)r.GetInt16(3),
                StartedAt = r.GetDateTime(4),
                ExpiresAt = r.GetDateTime(5),
                PricePaid = r.GetDecimal(6),
                Status = (PromotionStatus)r.GetInt16(7)
            },
            p =>
            {
                p.AddWithValue("@UserId", userId);
                p.AddWithValue("@Active", (short)PromotionStatus.Active);
            });
        public async Task<int> CreateAsync(int userId, PromotionTier tier, PromotionTermType termType, DateTime startedAt, DateTime expiresAt, decimal pricePaid, int? paymentId = null) => (await QueryAsync<int>(
            @"INSERT INTO Promotion (UserId, Tier, TermType, StartedAt, ExpiresAt, PricePaid, Status, PaymentId)
              OUTPUT INSERTED.Id VALUES (@UserId, @Tier, @TermType, @StartedAt, @ExpiresAt, @PricePaid, @Active, @PaymentId)",
            r => r.GetInt32(0),
            p =>
            {
                p.AddWithValue("@UserId", userId);
                p.AddWithValue("@Tier", (short)tier);
                p.AddWithValue("@TermType", (short)termType);
                p.AddWithValue("@StartedAt", startedAt);
                p.AddWithValue("@ExpiresAt", expiresAt);
                p.AddWithValue("@PricePaid", pricePaid);
                p.AddWithValue("@Active", (short)PromotionStatus.Active);
                p.AddWithValue("@PaymentId", (object?)paymentId ?? DBNull.Value);
            }))[0];
        public async Task SupersedeAsync(int promotionId) => await ExecuteAsync("UPDATE Promotion SET Status = @Superseded WHERE Id = @Id",
            p =>
            {
                p.AddWithValue("@Superseded", (short)PromotionStatus.Superseded);
                p.AddWithValue("@Id", promotionId);
            });
        public async Task<int> ExpireDueAsync() => await ExecuteAsync("UPDATE Promotion SET Status = @Expired WHERE Status = @Active AND ExpiresAt <= SYSUTCDATETIME()",
            p =>
            {
                p.AddWithValue("@Expired", (short)PromotionStatus.Expired);
                p.AddWithValue("@Active", (short)PromotionStatus.Active);
            });
    }
}