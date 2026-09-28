using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public interface IPointsLedgerRepository
    {
        Task<int?> TryAddAsync(int userId, PointsLedgerEventType eventType, int? sourceUserId = null, int? sourceProductId = null);
        Task RecordListingPostedAsync(int sellerId, int productId);
        /// <summary>Вариант для выполнения внутри внешней транзакции (массовый импорт).</summary>
        Task RecordListingPostedAsync(SqlConnection conn, SqlTransaction tx, int sellerId, int productId);
        Task<PointsSummary> GetSummaryAsync(int userId);
        Task<int> PromoteDueEntriesAsync();
        Task<List<PointsLedgerEntry>> GetHistoryAsync(int userId, int limit = 50);
        Task<(int Earned, int Pending)> GetReferralPointsAsync(int userId);
    }
}