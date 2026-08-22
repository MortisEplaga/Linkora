using Linkora.Models;

namespace Linkora.Repositories
{
    public interface IAdminRepository
    {
        Task<AdminBadges> GetSidebarBadgesAsync();
        Task<AdminDashboardViewModel> GetDashboardStatsAsync();
        Task<PagedResult<AdminProductRow>> GetProductsAsync(string status, int page, string? search);
        Task<PagedResult<AdminUserRow>> GetUsersAsync(int page, string? search, string role);
        Task<PagedResult<AdminReportRow>> GetReportsAsync(string status, int page);
        Task<AdminStatsApiData> GetStatsApiDataAsync();
        Task<int?> SetProductStatusAsync(int id, string status);
        Task<string> ResolveReportAsync(int id, string action);
        Task<RejectProductResult> RejectProductWithReasonAsync(int id, int reasonId, string? comment);
        Task<string?> UpdateUserRoleAsync(int id, string role);
        Task<List<int>> GetSubscriberIdsAsync(int userId);
        Task<List<(int UserId, int ProductId)>> GetFavouriteUsersBySellerAsync(int sellerId);
        Task<List<int>> GetUserProductIdsAsync(int userId);
        Task DeleteUserAsync(int id);
        Task<ApproveOptionResult> GetApproveOptionContextAsync(int optionId);
        Task DecrementModerationScoreAsync(int productId);
        Task<RejectOptionResult> GetRejectOptionContextAsync(int optionId, int productId);
    }
}