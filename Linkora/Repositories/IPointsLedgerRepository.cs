using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IPointsLedgerRepository
    {
        Task<int?> TryAddAsync(int userId, PointsLedgerEventType eventType, int? sourceUserId = null, int? sourceProductId = null);
        Task RecordListingPostedAsync(int sellerId, int productId);
        Task<PointsSummary> GetSummaryAsync(int userId);
        Task<int> PromoteDueEntriesAsync();
        Task<List<PointsLedgerEntry>> GetHistoryAsync(int userId, int limit = 50);
    }
}