using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IAdminRepository
    {
        Task<AdminBadges> GetSidebarBadgesAsync();
        Task<AdminDashboardViewModel> GetDashboardStatsAsync();
        Task<PagedResult<AdminProductRow>> GetProductsAsync(string status, int page, string? search);
        Task<int?> SetProductStatusAsync(int id, string status);
        Task<PagedResult<AdminUserRow>> GetUsersAsync(int page, string? search, string role);
        Task<(string? oldRole, BanUserResult? banData)> SetUserRoleAsync(int id, string role);
        Task DeleteUserAsync(int id);
        Task<PagedResult<AdminReportRow>> GetReportsAsync(string status, int page);
        Task<string> ResolveReportAsync(int id, string action);
        Task<AdminStatsApiData> GetStatsApiDataAsync();
        Task<ApproveOptionResult> ApproveOptionAsync(int id);
        Task<RejectOptionResult> RejectProductByOptionAsync(int optionId, int productId);
        Task<RejectProductResult> RejectProductWithReasonAsync(int id, int reasonId, string? comment);
    }
}