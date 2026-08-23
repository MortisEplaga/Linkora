using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class ReportRepository : SqlRepositoryBase, IReportRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public ReportRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) : base(configuration)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        private string GetLang() => _httpContextAccessor.HttpContext?.Request.Cookies["lang"] ?? "en";
        public async Task<List<ReportReasonLocalized>> GetActiveReasonsLocalizedAsync()
        {
            return await QueryAsync(@"
                SELECT Id, ReasonText, ReasonTextLV, ReasonTextRU
                FROM ReportReasons WHERE IsActive = 1 ORDER BY ReasonText", r =>
            {
                var en = r.GetString(1);
                return new ReportReasonLocalized
                {
                    Id = r.GetInt32(0),
                    Text = Resolve(GetLang(), en, r.GetStringOrDefault(2, en), r.GetStringOrDefault(3, en))
                };
            });
        }
        public async Task<ReportReason?> GetReasonByIdAsync(int reasonId)
        {
            return await QuerySingleAsync(@"
                SELECT Id, ReasonText, ReasonTextLV, ReasonTextRU
                FROM ReportReasons
                WHERE Id = @Id", r => new ReportReason
            {
                Id = r.GetInt32(0),
                ReasonText = r.GetString(1),
                ReasonTextLV = r.GetStringOrDefault(2, r.GetString(1)),
                ReasonTextRU = r.GetStringOrDefault(3, r.GetString(1)),
            }, p => p.AddWithValue("@Id", reasonId));
        }
        public async Task<Report> CreateReportAsync(int productId, int userId, int reportReasonId, string? comment)
        {
            var ids = await QueryAsync<int>(
                @"INSERT INTO Reports (ProductId, UserId, ReportReasonId, Comment, CreatedAt, Status)
                  OUTPUT INSERTED.Id
                  VALUES (@ProductId, @UserId, @ReportReasonId, @Comment, @CreatedAt, @Status)",
                r => r.GetInt32(0),
                p =>
                {
                    p.AddWithValue("@ProductId", productId);
                    p.AddWithValue("@UserId", userId);
                    p.AddWithValue("@ReportReasonId", reportReasonId);
                    p.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
                    p.AddWithValue("@CreatedAt", DateTime.Now);
                    p.AddWithValue("@Status", ReportStatus.Pending.ToString());
                });

            var id = ids[0];

            await ExecuteAsync(@"
                UPDATE Products 
                SET ModerationScore = ModerationScore + 1,
                    Status = CASE WHEN ModerationScore + 1 >= 5 THEN 'Moderation' ELSE Status END
                WHERE Id = @ProductId AND Status NOT IN ('Moderation', 'Rejected', 'Archived', 'Succeeded')",
                p => p.AddWithValue("@ProductId", productId));

            return new Report
            {
                Id = id,
                ProductId = productId,
                UserId = userId,
                ReportReasonId = reportReasonId,
                Comment = comment,
                CreatedAt = DateTime.Now,
                Status = ReportStatus.Pending
            };
        }
        public async Task<IEnumerable<Report>> GetReportsByProductIdAsync(int productId)
        {
            return await QueryAsync(
                "SELECT * FROM Reports WHERE ProductId = @ProductId ORDER BY CreatedAt DESC",
                MapReport,
                p => p.AddWithValue("@ProductId", productId));
        }
        public async Task<IEnumerable<Report>> GetPendingReportsAsync()
        {
            return await QueryAsync(
                "SELECT * FROM Reports WHERE Status = 'Pending' ORDER BY CreatedAt ASC",
                MapReport);
        }
        public async Task UpdateReportStatusAsync(int reportId, ReportStatus status)
        {
            await ExecuteAsync(
                "UPDATE Reports SET Status = @Status WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Status", status.ToString());
                    p.AddWithValue("@Id", reportId);
                });
        }
        private Report MapReport(SqlDataReader reader)
        {
            return new Report
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                ReportReasonId = reader.GetInt32(reader.GetOrdinal("ReportReasonId")),
                Comment = reader.GetStringOrNull(reader.GetOrdinal("Comment")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Status = Enum.Parse<ReportStatus>(reader.GetString(reader.GetOrdinal("Status")))
            };
        }
    }
}