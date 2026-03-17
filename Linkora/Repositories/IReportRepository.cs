using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IReportRepository
    {
        Task<Report> CreateReportAsync(int productId, int userId, int reportReasonId, string? comment);
        Task<IEnumerable<Report>> GetReportsByProductIdAsync(int productId);
        Task<IEnumerable<Report>> GetPendingReportsAsync();
        Task UpdateReportStatusAsync(int reportId, ReportStatus status);
        Task<List<ReportReason>> GetActiveReportReasonsAsync(); // новый метод

    }
}