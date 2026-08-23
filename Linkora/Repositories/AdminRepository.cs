using Linkora.Models;

namespace Linkora.Repositories
{
    public class AdminRepository : SqlRepositoryBase, IAdminRepository
    {
        public AdminRepository(IConfiguration configuration) : base(configuration) { }
        public async Task<AdminBadges> GetSidebarBadgesAsync()
        {
            var badges = await QuerySingleAsync<AdminBadges>(@"
            SELECT 
            (SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'),
            (SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'),
            (SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0)",
                r => new AdminBadges
                {
                    PendingModeration = r.GetInt32(0),
                    PendingReports = r.GetInt32(1),
                    PendingOptions = r.GetInt32(2)
                });
            return badges ?? new AdminBadges();
        }
        public async Task<AdminDashboardViewModel> GetDashboardStatsAsync()
        {
            var stats = new AdminDashboardViewModel();
            await QueryAsync<AdminDashboardViewModel>(@"
                SELECT
                    (SELECT COUNT(*) FROM Users),
                    (SELECT COUNT(*) FROM Products),
                    (SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'),
                    (SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'),
                    (SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0),
                    (SELECT COUNT(*) FROM Users WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)),
                    (SELECT COUNT(*) FROM Products WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)),
                    (SELECT COUNT(*) FROM Products WHERE Status = 'Active')",
                r =>
                {
                    stats.TotalUsers = r.GetInt32(0);
                    stats.TotalProducts = r.GetInt32(1);
                    stats.PendingModeration = r.GetInt32(2);
                    stats.PendingReports = r.GetInt32(3);
                    stats.PendingOptions = r.GetInt32(4);
                    stats.NewUsersToday = r.GetInt32(5);
                    stats.NewProductsToday = r.GetInt32(6);
                    stats.ActiveProducts = r.GetInt32(7);
                    return stats;
                });
            await QueryAsync<string>("SELECT Status, COUNT(*) FROM Products GROUP BY Status",
                r => { stats.ProductsByStatus[r.GetString(0)] = r.GetInt32(1); return null!; });
            stats.RecentProducts.AddRange(await QueryAsync<AdminProductRow>(@"
                SELECT TOP 10 p.Id, p.Name, p.Status, p.CreatedAt, u.UserName
                FROM Products p
                LEFT JOIN Users u ON u.Id = p.UserId
                ORDER BY p.CreatedAt DESC",
                r => new AdminProductRow
                {
                    Id = r.GetInt32(0),
                    Name = r.GetStringOrDefault(1),
                    Status = r.GetStringOrDefault(2),
                    CreatedAt = r.GetDateTimeOrNull(3),
                    UserName = r.GetStringOrDefault(4),
                }));
            return stats;
        }
        public async Task<PagedResult<AdminProductRow>> GetProductsAsync(string status, int page, string? search)
        {
            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND p.Name LIKE '%' + @Search + '%'";
            return await GetPagedDataAsync(
                selectClause: @"
                    SELECT p.Id, p.Name, p.Status, p.CreatedAt,
                           COALESCE(
                               (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                               p.AvatarUrl
                           ) AS Img,
                           u.UserName, u.Id AS UserId,
                           (SELECT COUNT(*) FROM Reports WHERE ProductId = p.Id) AS ReportCount,
                           (SELECT TOP 1 TRY_CAST(m.Value AS decimal(18,2))
                            FROM MapperProductCategory m
                            JOIN Category c ON c.Id = m.CategoryId AND c.Name = 'Price, €'
                            WHERE m.ProductId = p.Id) AS Price",
                fromWhereClause: $"FROM Products p LEFT JOIN Users u ON u.Id = p.UserId WHERE p.Status = @Status {searchClause}",
                orderByClause: "ORDER BY p.CreatedAt DESC",
                page: page, pageSize: 20,
                addParameters: p =>
                {
                    p.AddWithValue("@Status", status);
                    if (!string.IsNullOrEmpty(search)) p.AddWithValue("@Search", search);
                },
                mapRow: r => new AdminProductRow
                {
                    Id = r.GetInt32(0),
                    Name = r.GetStringOrDefault(1),
                    Status = r.GetStringOrDefault(2),
                    CreatedAt = r.GetDateTimeOrNull(3),
                    AvatarUrl = r.GetStringOrNull(4),
                    UserName = r.GetStringOrDefault(5),
                    UserId = r.GetInt32OrDefault(6),
                    ReportCount = r.GetInt32OrDefault(7),
                    Price = r.GetDecimalOrNull(8),
                });
        }
        public async Task<int?> SetProductStatusAsync(int id, string status)
        {
            return status != "Active" ? null : (await QueryAsync<int?>("UPDATE Products SET Status = @S OUTPUT inserted.UserId WHERE Id = @Id",
                r => r.GetInt32OrNull(0),
                p => { p.AddWithValue("@S", status); p.AddWithValue("@Id", id); })).FirstOrDefault();
        }
        public async Task<PagedResult<AdminUserRow>> GetUsersAsync(int page, string? search, string role)
        {
            var roleClause = role == "all" ? "" : "AND Role = @Role";
            var searchClause = string.IsNullOrEmpty(search) ? "" : "AND (UserName LIKE '%' + @Search + '%' OR Email LIKE '%' + @Search + '%')";
            return await GetPagedDataAsync(
                selectClause: @"
                    SELECT u.Id, u.UserName, u.Email, u.Phone, u.Role, u.IsCompany,
                           u.AvatarUrl, u.CreatedAt,
                           (SELECT COUNT(*) FROM Products WHERE UserId = u.Id) AS ProductCount",
                fromWhereClause: $"FROM Users u WHERE 1=1 {roleClause} {searchClause}",
                orderByClause: "ORDER BY u.CreatedAt DESC",
                page: page, pageSize: 25,
                addParameters: p =>
                {
                    if (role != "all") p.AddWithValue("@Role", role);
                    if (!string.IsNullOrEmpty(search)) p.AddWithValue("@Search", search);
                },
                mapRow: r => new AdminUserRow
                {
                    Id = r.GetInt32(0),
                    UserName = r.GetStringOrDefault(1),
                    Email = r.GetStringOrNull(2),
                    Phone = r.GetStringOrNull(3),
                    Role = r.GetStringOrDefault(4, "user"),
                    IsCompany = r.GetBooleanOrDefault(5),
                    AvatarUrl = r.GetStringOrNull(6),
                    CreatedAt = r.GetDateTimeOrNull(7),
                    ProductCount = r.GetInt32OrDefault(8),
                });
        }
        public async Task<string?> UpdateUserRoleAsync(int id, string role)
        {
            return await QuerySingleAsync<string>(
                "UPDATE Users SET Role = @R OUTPUT deleted.Role WHERE Id = @Id",
                r => r.IsDBNull(0) ? null! : r.GetString(0),
                p => { p.AddWithValue("@R", role); p.AddWithValue("@Id", id); });
        }
        public Task<List<int>> GetSubscriberIdsAsync(int userId)
        {
            return QueryAsync("SELECT FollowerId FROM Subscriptions WHERE FollowingId = @Id",
                r => r.GetInt32(0),
                p => p.AddWithValue("@Id", userId));
        }
        public Task<List<(int UserId, int ProductId)>> GetFavouriteUsersBySellerAsync(int sellerId)
        {
            return QueryAsync(
                @"SELECT DISTINCT f.UserId, f.ProductId
                  FROM Favourites f
                  JOIN Products p ON f.ProductId = p.Id
                  WHERE p.UserId = @Id AND f.Can = 1",
                r => (UserId: r.GetInt32(0), ProductId: r.GetInt32(1)),
                p => p.AddWithValue("@Id", sellerId));
        }
        public Task<List<int>> GetUserProductIdsAsync(int userId)
        {
            return QueryAsync("SELECT Id FROM Products WHERE UserId = @UserId",
                r => r.GetInt32(0),
                p => p.AddWithValue("@UserId", userId));
        }
        public async Task DeleteUserAsync(int id) => await ExecuteAsync("DELETE FROM Users WHERE Id = @Id", p => p.AddWithValue("@Id", id));
        public async Task<PagedResult<AdminReportRow>> GetReportsAsync(string status, int page)
        {
            return await GetPagedDataAsync(
                selectClause: @"
                    SELECT r.Id, r.ProductId, r.UserId, r.Comment, r.CreatedAt, r.Status,
                           p.Name AS ProductName,
                           COALESCE(
                               (SELECT TOP 1 pm.FilePath FROM ProductMedia pm WHERE pm.ProductId = p.Id ORDER BY pm.SortOrder),
                               p.AvatarUrl
                           ) AS ProductImg,
                           p.Status AS ProductStatus,
                           u.UserName AS ReporterName,
                           rr.ReasonText",
                fromWhereClause: @"
                    FROM Reports r
                    LEFT JOIN Products p ON p.Id = r.ProductId
                    LEFT JOIN Users u ON u.Id = r.UserId
                    LEFT JOIN ReportReasons rr ON rr.Id = r.ReportReasonId
                    WHERE r.Status = @Status",
                orderByClause: "ORDER BY r.CreatedAt DESC",
                page: page, pageSize: 20,
                addParameters: p => p.AddWithValue("@Status", status),
                mapRow: r => new AdminReportRow
                {
                    Id = r.GetInt32(0),
                    ProductId = r.GetInt32(1),
                    UserId = r.GetInt32(2),
                    Comment = r.GetStringOrNull(3),
                    CreatedAt = r.GetDateTime(4),
                    Status = r.GetString(5),
                    ProductName = r.GetStringOrDefault(6),
                    ProductImage = r.GetStringOrNull(7),
                    ProductStatus = r.GetStringOrDefault(8),
                    ReporterName = r.GetStringOrDefault(9),
                    ReasonText = r.GetStringOrDefault(10),
                });
        }
        public async Task<string> ResolveReportAsync(int id, string action)
        {
            var newStatus = action == "resolve" ? "Resolved" : "Rejected";
            await ExecuteAsync("UPDATE Reports SET Status = @S WHERE Id = @Id",
                p => { p.AddWithValue("@S", newStatus); p.AddWithValue("@Id", id); });
            if (action == "reject_report")
                await ExecuteAsync(@"UPDATE Products SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END, Status = CASE WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 THEN 'Active' ELSE Status END WHERE Id = (SELECT ProductId FROM Reports WHERE Id = @Id) AND Status IN ('Active', 'Moderation')",
                    p => p.AddWithValue("@Id", id));
            return newStatus;
        }
        public async Task<AdminStatsApiData> GetStatsApiDataAsync()
        {
            var data = new AdminStatsApiData();
            const string sql = @"
                SELECT CAST(CreatedAt AS DATE) AS Day, COUNT(*) AS Cnt
                FROM {0}
                WHERE CreatedAt >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY Day";
            data.Registrations.AddRange(await QueryAsync(string.Format(sql, "Users"),
                r => new { day = r.GetDateTime(0).ToString("dd MMM"), count = r.GetInt32(1) }));
            data.Products.AddRange(await QueryAsync(string.Format(sql, "Products"),
                r => new { day = r.GetDateTime(0).ToString("dd MMM"), count = r.GetInt32(1) }));
            return data;
        }
        public async Task<ApproveOptionResult> GetApproveOptionContextAsync(int optionId)
        {
            var result = new ApproveOptionResult();
            await QueryAsync<ApproveOptionResult>(@"
                SELECT TOP 1 p.Id, p.UserId, c.Name, c.NameRU, c.NameLV
                FROM SelectOptions so
                JOIN MapperProductCategory mpc ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%'
                JOIN Products p ON mpc.ProductId = p.Id
                JOIN Category c ON so.CategoryId = c.Id
                WHERE so.Id = @OptionId",
                r =>
                {
                    result.ProductId = r.GetInt32(0);
                    result.UserId = r.GetInt32OrNull(1);
                    result.ParamName = r.GetStringOrNull(2);
                    result.ParamNameRu = r.IsDBNull(3) ? result.ParamName : r.GetString(3);
                    result.ParamNameLv = r.IsDBNull(4) ? result.ParamName : r.GetString(4);
                    return result;
                },
                p => p.AddWithValue("@OptionId", optionId));
            return result;
        }
        public async Task DecrementModerationScoreAsync(int productId)
            => await ExecuteAsync(@"UPDATE Products
                SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END,
                    Status = CASE WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 THEN 'Active' ELSE Status END
                WHERE Id = @ProductId",
                p => p.AddWithValue("@ProductId", productId));
        public async Task<RejectOptionResult> GetRejectOptionContextAsync(int optionId, int productId)
        {
            var result = new RejectOptionResult();
            await QueryAsync<RejectOptionResult>(@"
                SELECT p.UserId, c.Name, c.NameRU, c.NameLV
                FROM SelectOptions so
                JOIN Category c ON so.CategoryId = c.Id
                JOIN Products p ON p.Id = @ProductId
                WHERE so.Id = @OptionId",
                r =>
                {
                    result.UserId = r.GetInt32OrNull(0);
                    result.ParamName = r.GetStringOrNull(1);
                    result.ParamNameRu = r.IsDBNull(2) ? result.ParamName : r.GetString(2);
                    result.ParamNameLv = r.IsDBNull(3) ? result.ParamName : r.GetString(3);
                    return result;
                },
                p => { p.AddWithValue("@OptionId", optionId); p.AddWithValue("@ProductId", productId); });
            return result;
        }
        public async Task<RejectProductResult> RejectProductWithReasonAsync(int id, int reasonId, string? comment)
        {
            var result = new RejectProductResult { InvalidReason = true };
            await QueryAsync<RejectProductResult>(@"
                SELECT rr.ReasonText, rr.ReasonTextLV, rr.ReasonTextRU, p.UserId
                FROM ReportReasons rr
                LEFT JOIN Products p ON p.Id = @Id
                WHERE rr.Id = @ReasonId",
                r =>
                {
                    result.InvalidReason = false;
                    if (r.IsDBNull(3)) return result;
                    result.ReasonEn = r.GetStringOrDefault(0);
                    result.ReasonLv = r.IsDBNull(1) ? result.ReasonEn : r.GetString(1);
                    result.ReasonRu = r.IsDBNull(2) ? result.ReasonEn : r.GetString(2);
                    result.UserId = r.GetInt32(3);
                    return result;
                },
                p => { p.AddWithValue("@Id", id); p.AddWithValue("@ReasonId", reasonId); });
            if (result.InvalidReason) return result;
            await ExecuteAsync("UPDATE Products SET Status = 'Rejected' WHERE Id = @Id", p => p.AddWithValue("@Id", id));
            result.Success = true;
            result.Comment = comment ?? "";
            return result;
        }
    }
}