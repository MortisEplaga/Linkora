using Linkora.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Linkora.Repositories
{
    public class ReportRepository : SqlRepositoryBase, IReportRepository
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;
        public ReportRepository(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IMemoryCache cache) : base(configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }
        public async Task<List<ReportReasonLocalized>> GetActiveReasonsLocalizedAsync()
        {
            var cacheKey = $"report_reasons_active_{_httpContextAccessor.HttpContext.GetLang()}";

            if (_cache.TryGetValue(cacheKey, out List<ReportReasonLocalized>? cached) && cached != null) return cached;

            var result = await QueryAsync(@"SELECT Id, ReasonText, ReasonTextLV, ReasonTextRU
                                            FROM ReportReasons WHERE IsActive = 1 ORDER BY ReasonText", r =>
            {
                var en = r.GetString(1);
                return new ReportReasonLocalized
                {
                    Id = r.GetInt32(0),
                    Text = Resolve(_httpContextAccessor.HttpContext.GetLang(), en, r.GetStringOrDefault(2, en), r.GetStringOrDefault(3, en))
                };
            });
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            return result;
        }
        public async Task<ReportReason?> GetReasonByIdAsync(int reasonId)
        {
            var cacheKey = $"report_reason_{reasonId}";
            if (_cache.TryGetValue(cacheKey, out ReportReason? cached) && cached != null) return cached;

            var result = await QuerySingleAsync(@"SELECT Id, ReasonText, ReasonTextLV, ReasonTextRU
                                                  FROM ReportReasons
                                                  WHERE Id = @Id", r => new ReportReason
            {
                Id = r.GetInt32(0),
                ReasonText = r.GetString(1),
                ReasonTextLV = r.GetStringOrDefault(2, r.GetString(1)),
                ReasonTextRU = r.GetStringOrDefault(3, r.GetString(1)),
            }, p => p.AddWithValue("@Id", reasonId));

            if (result != null) _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            return result;
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
        public async Task<IEnumerable<Report>> GetReportsByProductIdAsync(int productId) => await QueryAsync(
                "SELECT * FROM Reports WHERE ProductId = @ProductId ORDER BY CreatedAt DESC",
                MapReport,
                p => p.AddWithValue("@ProductId", productId));
        public async Task<IEnumerable<Report>> GetPendingReportsAsync() => await QueryAsync(
                "SELECT * FROM Reports WHERE Status = 'Pending' ORDER BY CreatedAt ASC",
                MapReport);
        public async Task UpdateReportStatusAsync(int reportId, ReportStatus status) => await ExecuteAsync(
                "UPDATE Reports SET Status = @Status WHERE Id = @Id",
                p =>
                {
                    p.AddWithValue("@Status", status.ToString());
                    p.AddWithValue("@Id", reportId);
                });
        private Report MapReport(SqlDataReader reader) => new Report
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                ReportReasonId = reader.GetInt32(reader.GetOrdinal("ReportReasonId")),
                Comment = reader.GetStringOrNull(reader.GetOrdinal("Comment")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Status = Enum.Parse<ReportStatus>(reader.GetString(reader.GetOrdinal("Status")))
            };
        private async Task InvalidateReportReasonsCache()
        {
            _cache.Remove("report_reasons_active_en");
            _cache.Remove("report_reasons_active_lv");
            _cache.Remove("report_reasons_active_ru");
        }
    }
}