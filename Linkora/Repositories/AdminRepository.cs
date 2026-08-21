using Linkora.Models;
using Microsoft.Data.SqlClient;

namespace Linkora.Repositories
{
    public class AdminRepository : SqlRepositoryBase, IAdminRepository
    {
        private readonly IProductRepository _productRepository;
        public AdminRepository(IConfiguration configuration, IProductRepository productRepository) : base(configuration)
            => _productRepository = productRepository;
        public async Task<AdminBadges> GetSidebarBadgesAsync()
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(@"
                SELECT 
                    (SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'),
                    (SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'),
                    (SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0)", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return new AdminBadges
            {
                PendingModeration = r.GetInt32(0),
                PendingReports = r.GetInt32(1),
                PendingOptions = r.GetInt32(2)
            };
        }
        public async Task<AdminDashboardViewModel> GetDashboardStatsAsync()
        {
            await using var conn = await OpenConnectionAsync();
            var stats = new AdminDashboardViewModel();

            await using (var cmd = new SqlCommand(@"
                SELECT
                    (SELECT COUNT(*) FROM Users),
                    (SELECT COUNT(*) FROM Products),
                    (SELECT COUNT(*) FROM Products WHERE Status = 'Moderation'),
                    (SELECT COUNT(*) FROM Reports WHERE Status = 'Pending'),
                    (SELECT COUNT(*) FROM SelectOptions WHERE IsConf = 0),
                    (SELECT COUNT(*) FROM Users WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)),
                    (SELECT COUNT(*) FROM Products WHERE CAST(CreatedAt AS DATE) = CAST(GETDATE() AS DATE)),
                    (SELECT COUNT(*) FROM Products WHERE Status = 'Active')", conn))
            await using (var r = await cmd.ExecuteReaderAsync())
            {
                await r.ReadAsync();
                stats.TotalUsers = r.GetInt32(0);
                stats.TotalProducts = r.GetInt32(1);
                stats.PendingModeration = r.GetInt32(2);
                stats.PendingReports = r.GetInt32(3);
                stats.PendingOptions = r.GetInt32(4);
                stats.NewUsersToday = r.GetInt32(5);
                stats.NewProductsToday = r.GetInt32(6);
                stats.ActiveProducts = r.GetInt32(7);
            }

            await using (var cmd = new SqlCommand("SELECT Status, COUNT(*) FROM Products GROUP BY Status", conn))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    stats.ProductsByStatus[r.GetString(0)] = r.GetInt32(1);

            await using (var cmd = new SqlCommand(@"
                SELECT TOP 10 p.Id, p.Name, p.Status, p.CreatedAt, u.UserName
                FROM Products p
                LEFT JOIN Users u ON u.Id = p.UserId
                ORDER BY p.CreatedAt DESC", conn))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    stats.RecentProducts.Add(new AdminProductRow
                    {
                        Id = r.GetInt32(0),
                        Name = r.IsDBNull(1) ? "" : r.GetString(1),
                        Status = r.IsDBNull(2) ? "" : r.GetString(2),
                        CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                        UserName = r.IsDBNull(4) ? "" : r.GetString(4),
                    });

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
                    Name = r.IsDBNull(1) ? "" : r.GetString(1),
                    Status = r.IsDBNull(2) ? "" : r.GetString(2),
                    CreatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    AvatarUrl = r.IsDBNull(4) ? null : r.GetString(4),
                    UserName = r.IsDBNull(5) ? "" : r.GetString(5),
                    UserId = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                    ReportCount = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                    Price = r.IsDBNull(8) ? null : r.GetDecimal(8),
                });
        }
        public async Task<int?> SetProductStatusAsync(int id, string status)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("UPDATE Products SET Status = @S OUTPUT inserted.UserId WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@S", status);
            cmd.Parameters.AddWithValue("@Id", id);
            var owner = await cmd.ExecuteScalarAsync();
            return status == "Active" && owner != null && owner != DBNull.Value ? (int)owner : null;
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
                    UserName = r.IsDBNull(1) ? "" : r.GetString(1),
                    Email = r.IsDBNull(2) ? null : r.GetString(2),
                    Phone = r.IsDBNull(3) ? null : r.GetString(3),
                    Role = r.IsDBNull(4) ? "user" : r.GetString(4),
                    IsCompany = !r.IsDBNull(5) && r.GetBoolean(5),
                    AvatarUrl = r.IsDBNull(6) ? null : r.GetString(6),
                    CreatedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
                    ProductCount = r.IsDBNull(8) ? 0 : r.GetInt32(8),
                });
        }
        public async Task<(string? oldRole, BanUserResult? banData)> SetUserRoleAsync(int id, string role)
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand("UPDATE Users SET Role = @R OUTPUT deleted.Role WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@R", role);
            cmd.Parameters.AddWithValue("@Id", id);
            var oldRole = await cmd.ExecuteScalarAsync() as string;

            BanUserResult? banData = null;
            if (role == "banned" && oldRole != "banned")
            {
                await _productRepository.ArchiveProductsByUserAsync(id);
                banData = new BanUserResult();

                await using var subsCmd = new SqlCommand("SELECT FollowerId FROM Subscriptions WHERE FollowingId = @Id", conn);
                subsCmd.Parameters.AddWithValue("@Id", id);
                await using (var subsR = await subsCmd.ExecuteReaderAsync())
                    while (await subsR.ReadAsync()) banData.SubscriberIds.Add(subsR.GetInt32(0));

                await using var favCmd = new SqlCommand(@"SELECT DISTINCT f.UserId, f.ProductId FROM Favourites f JOIN Products p ON f.ProductId = p.Id WHERE p.UserId = @Id AND f.Can = 1", conn);
                favCmd.Parameters.AddWithValue("@Id", id);
                await using (var favR = await favCmd.ExecuteReaderAsync())
                    while (await favR.ReadAsync()) banData.FavouriteUsers.Add((favR.GetInt32(0), favR.GetInt32(1)));
            }
            return (oldRole, banData);
        }
        public async Task DeleteUserAsync(int id)
        {
            var productIds = await QueryAsync(
                "SELECT Id FROM Products WHERE UserId = @UserId",
                r => r.GetInt32(0),
                p => p.AddWithValue("@UserId", id));

            foreach (var productId in productIds)
                await _productRepository.DeleteAsync(productId);

            await ExecuteAsync("DELETE FROM Users WHERE Id = @Id", p => p.AddWithValue("@Id", id));
        }
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
                    Comment = r.IsDBNull(3) ? null : r.GetString(3),
                    CreatedAt = r.GetDateTime(4),
                    Status = r.GetString(5),
                    ProductName = r.IsDBNull(6) ? "" : r.GetString(6),
                    ProductImage = r.IsDBNull(7) ? null : r.GetString(7),
                    ProductStatus = r.IsDBNull(8) ? "" : r.GetString(8),
                    ReporterName = r.IsDBNull(9) ? "" : r.GetString(9),
                    ReasonText = r.IsDBNull(10) ? "" : r.GetString(10),
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
            await using var conn = await OpenConnectionAsync();

            const string sql = @"
                SELECT CAST(CreatedAt AS DATE) AS Day, COUNT(*) AS Cnt
                FROM {0}
                WHERE CreatedAt >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY Day";

            await using (var cmd = new SqlCommand(string.Format(sql, "Users"), conn))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    data.Registrations.Add(new { day = r.GetDateTime(0).ToString("dd MMM"), count = r.GetInt32(1) });

            await using (var cmd = new SqlCommand(string.Format(sql, "Products"), conn))
            await using (var r = await cmd.ExecuteReaderAsync())
                while (await r.ReadAsync())
                    data.Products.Add(new { day = r.GetDateTime(0).ToString("dd MMM"), count = r.GetInt32(1) });

            return data;
        }
        public async Task<ApproveOptionResult> ApproveOptionAsync(int id)
        {
            var result = new ApproveOptionResult();
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(@"
                SELECT TOP 1 p.Id, p.UserId, c.Name, c.NameRU, c.NameLV
                FROM SelectOptions so
                JOIN MapperProductCategory mpc ON ',' + mpc.Value + ',' LIKE '%,' + CAST(so.Id AS VARCHAR) + ',%'
                JOIN Products p ON mpc.ProductId = p.Id
                JOIN Category c ON so.CategoryId = c.Id
                WHERE so.Id = @OptionId", conn);
            cmd.Parameters.AddWithValue("@OptionId", id);

            await using (var r = await cmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    result.ProductId = r.GetInt32(0);
                    result.UserId = r.IsDBNull(1) ? null : r.GetInt32(1);
                    result.ParamName = r.IsDBNull(2) ? null : r.GetString(2);
                    result.ParamNameRu = r.IsDBNull(3) ? result.ParamName : r.GetString(3);
                    result.ParamNameLv = r.IsDBNull(4) ? result.ParamName : r.GetString(4);
                }
            }

            result.Success = await _productRepository.ApproveSelectOptionAsync(id);
            if (result.Success && result.UserId.HasValue && result.ProductId.HasValue)
            {
                await using var scoreCmd = new SqlCommand(@"UPDATE Products SET ModerationScore = CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END, Status = CASE WHEN Status = 'Moderation' AND (CASE WHEN ModerationScore > 0 THEN ModerationScore - 1 ELSE 0 END) < 5 THEN 'Active' ELSE Status END WHERE Id = @ProductId", conn);
                scoreCmd.Parameters.AddWithValue("@ProductId", result.ProductId.Value);
                await scoreCmd.ExecuteNonQueryAsync();
            }
            return result;
        }
        public async Task<RejectOptionResult> RejectProductByOptionAsync(int optionId, int productId)
        {
            var result = new RejectOptionResult();
            await using var conn = await OpenConnectionAsync();
            await using var cmd = new SqlCommand(@"
                SELECT p.UserId, c.Name, c.NameRU, c.NameLV
                FROM SelectOptions so
                JOIN Category c ON so.CategoryId = c.Id
                JOIN Products p ON p.Id = @ProductId
                WHERE so.Id = @OptionId", conn);
            cmd.Parameters.AddWithValue("@OptionId", optionId);
            cmd.Parameters.AddWithValue("@ProductId", productId);

            await using (var r = await cmd.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    result.UserId = r.IsDBNull(0) ? null : r.GetInt32(0);
                    result.ParamName = r.IsDBNull(1) ? null : r.GetString(1);
                    result.ParamNameRu = r.IsDBNull(2) ? result.ParamName : r.GetString(2);
                    result.ParamNameLv = r.IsDBNull(3) ? result.ParamName : r.GetString(3);
                }
            }

            result.Success = await _productRepository.RejectProductAndOptionAsync(optionId, productId);
            return result;
        }
        public async Task<RejectProductResult> RejectProductWithReasonAsync(int id, int reasonId, string? comment)
        {
            var result = new RejectProductResult();
            await using (var conn = await OpenConnectionAsync())
            {
                await using var cmd = new SqlCommand(@"
                    SELECT rr.ReasonText, rr.ReasonTextLV, rr.ReasonTextRU, p.UserId
                    FROM ReportReasons rr
                    LEFT JOIN Products p ON p.Id = @Id
                    WHERE rr.Id = @ReasonId", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@ReasonId", reasonId);
                await using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) { result.InvalidReason = true; return result; }
                if (r.IsDBNull(3)) return result;
                result.ReasonEn = r.IsDBNull(0) ? "" : r.GetString(0);
                result.ReasonLv = r.IsDBNull(1) ? result.ReasonEn : r.GetString(1);
                result.ReasonRu = r.IsDBNull(2) ? result.ReasonEn : r.GetString(2);
                result.UserId = r.GetInt32(3);
            }

            await ExecuteAsync("UPDATE Products SET Status = 'Rejected' WHERE Id = @Id", p => p.AddWithValue("@Id", id));
            result.Success = true;
            result.Comment = comment ?? "";
            return result;
        }
    }
}